using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;

namespace Client.Services;

public class DriveService
{
	public event EventHandler<ExitCode>? Initialized;	/* Gives the exit code of the InitializeAsync method */
	public bool IsInitialized { get; private set; } = false;
	
	private readonly ClientService _clientService;
	private readonly ConcurrentDictionary<int, VmGeneralDescriptor> _virtualMachines;
	private readonly ConcurrentDictionary<int, DriveGeneralDescriptor> _drives;

	private readonly ConcurrentDictionary<int, HashSet<int>> _vmsByDriveId;
	private readonly ConcurrentDictionary<int, HashSet<int>> _drivesByVmId;

	public DriveService(ClientService clientService)
	{
		_clientService = clientService;
		_virtualMachines = new ConcurrentDictionary<int, VmGeneralDescriptor>();
		_drives = new ConcurrentDictionary<int, DriveGeneralDescriptor>();
		_vmsByDriveId = new ConcurrentDictionary<int, HashSet<int>>();
		_drivesByVmId = new ConcurrentDictionary<int, HashSet<int>>();
		
		_clientService.VmCreated += OnVmCreated;
		_clientService.VmDeleted += OnVmDeleted;
		_clientService.VmPoweredOn += OnVmPoweredOn;
		_clientService.VmPoweredOff += OnVmPoweredOffOrCrashed;
		_clientService.VmCrashed += OnVmPoweredOffOrCrashed;
		_clientService.DriveCreated += OnDriveCreated;
		_clientService.ItemDeleted += OnItemDeleted;
		_clientService.DriveConnected += OnDriveConnected;
		_clientService.DriveDisconnected += OnDriveDisconnected;
	}

	public async Task<ExitCode> InitializeAsync()
	{
		if (IsInitialized) return ExitCode.AlreadyInitialized;

		Task<ExitCode> fetchVms = FetchVmsAsync();
		Task<ExitCode> fetchDrives = FetchDrivesAsync();
		Task<ExitCode> fetchDriveConnections = FetchDriveConnectionsAsync();

		ExitCode[] results = await Task.WhenAll(fetchVms, fetchDrives, fetchDriveConnections);

		ExitCode result = results.Any(code => code != ExitCode.Success) ? ExitCode.DataFetchFailed : ExitCode.Success;
		
		if (result == ExitCode.Success) IsInitialized = true;

		Initialized?.Invoke(this, result);
		
		return result;
	}

	public async Task<ExitCode> ConnectDriveAsync(int driveId, int vmId)
	{
		MessageResponseConnectDrive.Status result = await _clientService.ConnectDriveAsync(driveId, vmId);

		if (result == MessageResponseConnectDrive.Status.Success 
		    || (result == MessageResponseConnectDrive.Status.AlreadyConnected && !ConnectionExists(driveId, vmId)))
		{
			AddConnection(driveId, vmId);
		}
		
		return result switch
		{
			MessageResponseConnectDrive.Status.Success				=> ExitCode.Success,
			MessageResponseConnectDrive.Status.AlreadyConnected		=> ExitCode.DriveConnectionAlreadyExists,
			MessageResponseConnectDrive.Status.Failure				=> ExitCode.MessageFailure,
		};
	}

	public async Task<ExitCode> DisconnectDriveAsync(int driveId, int vmId)
	{
		MessageResponseDisconnectDrive.Status result = await _clientService.DisconnectDriveAsync(driveId, vmId);

		if (result == MessageResponseDisconnectDrive.Status.Success 
		    || (result == MessageResponseDisconnectDrive.Status.NotConnected && ConnectionExists(driveId, vmId)))
		{
			RemoveConnection(driveId, vmId);
		}
		
		return result switch
		{
			MessageResponseDisconnectDrive.Status.Success		=> ExitCode.Success,
			MessageResponseDisconnectDrive.Status.NotConnected	=> ExitCode.DriveConnectionDoesNotExist,
			MessageResponseDisconnectDrive.Status.Failure		=> ExitCode.MessageFailure,
		};
	}

	public bool ConnectionExists(int driveId, int vmId)
	{
		if (_drivesByVmId.TryGetValue(vmId, out HashSet<int>? drives))
		{
			return drives.Contains(driveId);
		}

		return false;
	}
	
	public VmGeneralDescriptor[] GetVirtualMachines() => _virtualMachines.Values.ToArray();
	public DriveGeneralDescriptor[] GetDrives() => _drives.Values.ToArray();
	public DriveGeneralDescriptor? GetDriveById(int driveId) => _drives.GetValueOrDefault(driveId);

	public DriveGeneralDescriptor? GetDriveByName(string name)
	{
		foreach (DriveGeneralDescriptor drive in _drives.Values)
		{
			if (drive.Name == name) 
				return drive;
		}

		return null;
	}

	public bool DriveExists(string name) => GetDriveByName(name) != null;

	public DriveGeneralDescriptor[]? GetDrivesOnVirtualMachine(int vmId)
	{
		if (vmId < 1) return null;
		
		if (_drivesByVmId.TryGetValue(vmId, out HashSet<int>? driveIds))
		{
			DriveGeneralDescriptor[] drives = new DriveGeneralDescriptor[driveIds.Count];
			int i = 0;
			foreach (int driveId in driveIds)
			{
				drives[i++] = _drives[driveId];
			}
			
			return drives;
		}
		
		return null;
	}

	public VmGeneralDescriptor[]? GetVirtualMachinesOnDrive(int driveId)
	{
		if (driveId < 1) return null;
		
		if (_vmsByDriveId.TryGetValue(driveId, out HashSet<int>? vmIds))
		{
			VmGeneralDescriptor[] vms = new VmGeneralDescriptor[vmIds.Count];
			int i = 0;
			foreach (int vmId in vmIds)
			{
				vms[i++] = _virtualMachines[vmId];
			}

			return vms;
		}

		return null;
	}
	
	public bool IsDriveInUse(int driveId)
	{
		if (driveId < 1) return false;
		
		VmGeneralDescriptor[]? vms = GetVirtualMachinesOnDrive(driveId);
		if (vms == null) return false;

		return vms.Any(vm => vm.State == VmState.Running);
	}

	public async Task<PathItem[]?> ListItemsOnDrivePathAsync(int driveId, string path)
	{
		return await _clientService.ListItemsOnDrivePathAsync(driveId, path);
	}

	private async Task<ExitCode> FetchVmsAsync()
	{
		_virtualMachines.Clear();
		
		MessageResponseListVms? response = await _clientService.GetVirtualMachinesAsync();
		if (response == null || response.Result != MessageResponseListVms.Status.Success) return ExitCode.DataFetchFailed;
		
		foreach (VmGeneralDescriptor virtualMachine in response.Vms!)
		{
			_virtualMachines[virtualMachine.Id] = virtualMachine;
		}
		
		return ExitCode.Success;
	}

	private async Task<ExitCode> FetchDrivesAsync()
	{
		_drives.Clear();
		
		MessageResponseListDrives? response = await _clientService.GetDrivesAsync();
		if (response == null || response.Result != MessageResponseListDrives.Status.Success) return ExitCode.DataFetchFailed;

		foreach (DriveGeneralDescriptor drive in response.Drives!)
		{
			_drives[drive.Id] = drive;
		}
		
		return ExitCode.Success;
	}

	private async Task<ExitCode> FetchDriveConnectionsAsync()
	{
		_drivesByVmId.Clear();
		_vmsByDriveId.Clear();
		
		MessageResponseListDriveConnections? response = await _clientService.GetDriveConnectionsAsync();
		if (response == null || response.Result != MessageResponseListDriveConnections.Status.Success) return ExitCode.DataFetchFailed;

		foreach (DriveConnection connection in response.Connections!)
		{
			AddConnection(connection.DriveId, connection.VmId);
		}
		
		return ExitCode.Success;
	}
	
	private bool AddConnection(int driveId, int vmId)
	{
		if (ConnectionExists(driveId, vmId)) return false;

		if (!_drivesByVmId.TryGetValue(vmId, out HashSet<int>? drives))
		{
			drives = new HashSet<int>();
			_drivesByVmId[vmId] = drives;
		}
		drives.Add(driveId);

		if (!_vmsByDriveId.TryGetValue(driveId, out HashSet<int>? vms))
		{
			vms = new HashSet<int>();
			_vmsByDriveId[driveId] = vms;
		}
		vms.Add(vmId);
		
		return true;
	}

	private bool RemoveConnection(int driveId, int vmId)
	{
		if (!ConnectionExists(driveId, vmId)) return false;
		
		HashSet<int> drives = _drivesByVmId[vmId];
		drives.Remove(driveId);
		if (drives.Count == 0) _drivesByVmId.Remove(vmId, out _);
		
		HashSet<int> vms = _vmsByDriveId[driveId];
		vms.Remove(vmId);
		if (vms.Count == 0) _vmsByDriveId.Remove(driveId, out _);
		
		return true;
	}
	
	private void OnVmPoweredOffOrCrashed(object? sender, int vmId)
	{
		if (_virtualMachines.TryGetValue(vmId, out VmGeneralDescriptor? vm))
		{
			vm.State = VmState.ShutDown;
		}
	}

	private void OnVmPoweredOn(object? sender, int vmId)
	{
		if (_virtualMachines.TryGetValue(vmId, out VmGeneralDescriptor? vm))
		{
			vm.State = VmState.Running;
		}	
	}

	private void OnVmCreated(object? sender, VmGeneralDescriptor descriptor) =>
		_virtualMachines.TryAdd(descriptor.Id, descriptor);

	private void OnVmDeleted(object? sender, int vmId)
	{
		if (vmId < 1) return;
		
		if (_drivesByVmId.TryGetValue(vmId, out HashSet<int>? drives))
		{
			foreach (int driveId in drives)		/* Removes all instances of vmId in both _drivesByVmId and _vmsByDriveId */
			{
				RemoveConnection(driveId, vmId);
			}
		}
		
		_virtualMachines.TryRemove(vmId, out VmGeneralDescriptor? _);
	}

	private void OnDriveCreated(object? sender, DriveGeneralDescriptor descriptor) =>
		_drives.TryAdd(descriptor.Id, descriptor);

	private void OnItemDeleted(object? sender, MessageInfoItemDeleted info)
	{
		if (info.DriveId < 1 || !Common.IsPathToDrive(info.Path)) 
			return;
		
		if (_vmsByDriveId.TryGetValue(info.DriveId, out HashSet<int>? vms))
		{
			foreach (int vmId in vms)
			{
				RemoveConnection(info.DriveId, vmId);
			}
		}
		
		_drives.TryRemove(info.DriveId, out DriveGeneralDescriptor? _);
	}

	private void OnDriveConnected(object? sender, DriveConnection connection) =>
		AddConnection(connection.DriveId, connection.VmId);

	private void OnDriveDisconnected(object? sender, DriveConnection connection) =>
		RemoveConnection(connection.DriveId, connection.VmId);
}