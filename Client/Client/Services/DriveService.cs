using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Client.Services;

public class DriveService
{
	public event EventHandler<ExitCode>? Initialized;	/* Gives the exit code of the InitializeAsync method */
	public bool IsInitialized { get; private set; } = false;
	
	private readonly ClientService _clientService;
	private readonly ConcurrentDictionary<int, SharedDefinitions.VmGeneralDescriptor> _virtualMachines;
	private readonly ConcurrentDictionary<int, SharedDefinitions.DriveGeneralDescriptor> _drives;

	private readonly ConcurrentDictionary<int, HashSet<int>> _vmsByDriveId;
	private readonly ConcurrentDictionary<int, HashSet<int>> _drivesByVmId;
	
	public DriveService(ClientService clientService)
	{
		_clientService = clientService;
		_virtualMachines = new ConcurrentDictionary<int, SharedDefinitions.VmGeneralDescriptor>();
		_drives = new ConcurrentDictionary<int, SharedDefinitions.DriveGeneralDescriptor>();
		_vmsByDriveId = new ConcurrentDictionary<int, HashSet<int>>();
		_drivesByVmId = new ConcurrentDictionary<int, HashSet<int>>();
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

	public SharedDefinitions.VmGeneralDescriptor[] GetVirtualMachines() => _virtualMachines.Values.ToArray();
	public SharedDefinitions.DriveGeneralDescriptor[] GetDrives() => _drives.Values.ToArray();
	
	private async Task<ExitCode> FetchVmsAsync()
	{
		_virtualMachines.Clear();
		
		MessageResponseListVms? response = await _clientService.GetVirtualMachinesAsync();
		if (response == null || response.Result != MessageResponseListVms.Status.Success) return ExitCode.DataFetchFailed;
		
		foreach (SharedDefinitions.VmGeneralDescriptor virtualMachine in response.Vms!)
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

		foreach (SharedDefinitions.DriveGeneralDescriptor drive in response.Drives!)
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

		foreach (SharedDefinitions.DriveConnection connection in response.Connections!)
		{
			AddConnection(connection.DriveId, connection.VmId);
		}
		
		return ExitCode.Success;
	}
	
	private bool AddConnection(int driveId, int vmId)
	{
		if (ConnectionExists(driveId, vmId)) return false;

		if (!_drivesByVmId.TryGetValue(driveId, out HashSet<int>? drives))
		{
			drives = new HashSet<int>();
			_drivesByVmId[vmId] = drives;
		}
		drives.Add(vmId);

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
		
		HashSet<int> vms = _vmsByDriveId[driveId];
		vms.Remove(vmId);
		
		return true;
	}

	private bool ConnectionExists(int driveId, int vmId)
	{
		if (_drivesByVmId.TryGetValue(vmId, out HashSet<int>? drives))
		{
			return drives.Contains(driveId);
		}

		return false;
	}
}