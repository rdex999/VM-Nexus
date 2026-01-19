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

	/// <summary>
	/// Initializes this service. Fetches required data.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service is uninitialized. ClientService is initialized and connected to the server. <br/>
	/// Postcondition: On success, the service is initialized and the returned exit code indicaes success.
	/// On failure, the service is not initialized and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> InitializeAsync()
	{
		_virtualMachines.Clear();
		_drives.Clear();
		_vmsByDriveId.Clear();
		_drivesByVmId.Clear();
		
		Task<ExitCode> fetchVms = FetchVmsAsync();
		Task<ExitCode> fetchDrives = FetchDrivesAsync();
		Task<ExitCode> fetchDriveConnections = FetchDriveConnectionsAsync();

		await Task.WhenAll(fetchVms, fetchDrives, fetchDriveConnections);
		
		IsInitialized = true;

		Initialized?.Invoke(this, ExitCode.Success);
		
		return ExitCode.Success;
	}

	/// <summary>
	/// Connects the given drive to the given virtual machine. <br/>
	/// The drive will be connected to the virtual machine on next startup.
	/// </summary>
	/// <param name="driveId">The ID of the drive to connect to the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to connect the drive to. vmId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service initialized. The given drive and virtual machine exist, and are not connected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the given virtual machine and drive are connected (connection is registered,
	/// the virtual machine will have the drive connected on next startup) and the returned exit code indicates success. <br/>
	/// On failure, the connection state of the given virtual machine and drive is left unchanged and the returned exit code indicates the error.
	/// </remarks>
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

	/// <summary>
	/// Disconnects the given drive from the given virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to disconnect from the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to disconnect the drive from. vmId >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service initialized. The given virtual machine and drive exist, and are connected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the given virtual machine and drive are disconnected. (Connection is removed,
	/// the virtual machine will not have the drive connected to it on next startup) <br/>
	/// On failure, the connection state of the given virtual machine and drive is left unchanged and the returned exit code indicates the error.
	/// </remarks>
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

	/// <summary>
	/// Checks whether the given drive is connected to the given virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to check if the drive is connected to. vmId >= 1.</param>
	/// <returns>True if the drive is connected to the virtual machine, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized. The given virtual machine and drive exist. <br/>
	/// Postcondition: Returns true if the drive is connected to the virtual machine, false otherwise or on failure.
	/// </remarks>
	public bool ConnectionExists(int driveId, int vmId)
	{
		if (_drivesByVmId.TryGetValue(vmId, out HashSet<int>? drives))
		{
			return drives.Contains(driveId);
		}

		return false;
	}

	/// <summary>
	/// Gets all virtual machines of the user.
	/// </summary>
	/// <returns>General descriptors of all the virtual machines of the user.</returns>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: An array of general virtual machine descriptors of all the virtual machines of the user is returned.
	/// </remarks>
	public VmGeneralDescriptor[] GetVirtualMachines() => _virtualMachines.Values.ToArray();
	
	/// <summary>
	/// Gets all drives of the user.
	/// </summary>
	/// <returns>General drive descriptors of all the drives of the user.</returns>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: An array of general drive descriptors of all the drives of the user is returned.
	/// </remarks>
	public DriveGeneralDescriptor[] GetDrives() => _drives.Values.ToArray();
	
	/// <summary>
	/// Get a general drive descriptor of a drive by its ID.
	/// </summary>
	/// <param name="driveId">The ID of the drive to get the descriptor of. driveId >= 1.</param>
	/// <returns>A general descriptor of the drive, or null if the drive does not exist or is inaccessible.</returns>
	/// <remarks>
	/// Precondition: Service initialized. driveId >= 1.<br/>
	/// Postcondition: A general descriptor of the drive is returned, or null if the drive does not exist or is inaccessible.
	/// </remarks>
	public DriveGeneralDescriptor? GetDriveById(int driveId) => _drives.GetValueOrDefault(driveId);

	/// <summary>
	/// Get a general descriptor of a drive by its name.
	/// </summary>
	/// <param name="name">The name of the drive to get the descriptor of. name != null.</param>
	/// <returns>A general descriptor of the drive, or null if the user does not have such a drive.</returns>
	/// <remarks>
	/// Precondition: Service initialized. The user has a drive with the given name. name != null.<br/>
	/// Postcondition: On success, a general descriptor of the drive is returned. On failure, (user doesn't have the drive) null is returned.
	/// </remarks>
	public DriveGeneralDescriptor? GetDriveByName(string name)
	{
		foreach (DriveGeneralDescriptor drive in _drives.Values)
		{
			if (drive.Name == name) 
				return drive;
		}

		return null;
	}

	/// <summary>
	/// Checks if the user has a drive with the given name.
	/// </summary>
	/// <param name="name">The name of the drive to check for. name != null.</param>
	/// <returns>True if the drive exists, false otherwise.</returns>
	/// <remarks>
	/// Precondition: Service initialized. name != null. <br/>
	/// Postcondition: Returns true if the drive exists, false otherwise.
	/// </remarks>
	public bool DriveExists(string name) => GetDriveByName(name) != null;

	/// <summary>
	/// Get general descriptors of all drives that are connected to the given virtual machine.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine to get drives of. vmId >= 1.</param>
	/// <returns>
	/// An array of general drive descriptors, describing each drive that is connected to the virtual machine. Null is returned on failure.
	/// </returns>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine with the given ID exists under the user. vmId >= 1. <br/>
	/// Postcondition: On success, an array of general drive descriptors, describing each drive that is connected to the virtual machine.
	/// On failure, null is returned.
	/// </remarks>
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

	/// <summary>
	/// Get general descriptors of all virtual machines that are connected to a drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to get the virtual machines that are connected to. driveId >= 1.</param>
	/// <returns>An array of general virtual machine descriptors, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized. A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: On success, an array of general virtual machine descriptors of all virtual machines that
	/// are connected to the given drive is returned. On failure, null is returned.
	/// </remarks>
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

	/// <summary>
	/// Checks if the given drive is in use by any running virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to check if being used. driveId >= 1.</param>
	/// <returns>True if the given drive is in use, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized. A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: Returns true if the given drive is in use, false otherwise or on failure.
	/// </remarks>
	public bool IsDriveInUse(int driveId)
	{
		if (driveId < 1) return false;
		
		VmGeneralDescriptor[]? vms = GetVirtualMachinesOnDrive(driveId);
		if (vms == null) return false;

		return vms.Any(vm => vm.State == VmState.Running);
	}

	/// <summary>
	/// List items on the given path and drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to list the items under. driveId >= 1.</param>
	/// <param name="path">The path in the drive to list the items under. path != null.</param>
	/// <returns>An array of items, describing the items under the given drive and path. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: Service initialized. A drive with the given ID exists, the given path is valid and exists in the given drive.
	/// driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, an array of items is returned, describing the items under the given drive and path.
	/// On failure, null is returned
	/// </remarks>
	public async Task<PathItem[]?> ListItemsOnDrivePathAsync(int driveId, string path)
	{
		return await _clientService.ListItemsOnDrivePathAsync(driveId, path);
	}

	/// <summary>
	/// Fetches the users virtual machines.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: ClientService is connected to the server. Updating the list of the user's virtual machines is required.
	/// (for example, in service initialization) <br/>
	/// Postcondition: On success, the user's virtual machines are fetched and data structures are updated.
	/// On failure, datastructures stay unchanged and the returned exit code indicates the error.
	/// </remarks>
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

	/// <summary>
	/// Fetches the users drives.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: ClientService is connected to the server. Updating the list of the user's drives is required.
	/// (for example, in service initialization) <br/>
	/// Postcondition: On success, the user's drives are fetched and data structures are updated.
	/// On failure, data structures stay unchanged and the returned exit code indicates the error.
	/// </remarks>
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

	/// <summary>
	/// Fetches the users drive connections. (which virtual machines are conencted to which drives)
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: ClientService is connected to the server. Updating the list of the user's drive connections is required.
	/// (for example, in service initialization) <br/>
	/// Postcondition: On success, the user's drive connections are fetched and data structures are updated.
	/// On failure, data structures stay unchanged and the returned exit code indicates the error.
	/// </remarks>
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

	/// <summary>
	/// Adds a drive connection to local data structures.
	/// </summary>
	/// <param name="driveId">The ID of the drive that is not connected to the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive is now connected to. vmId >= 1.</param>
	/// <returns>True if the connection was added, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists, and a virtual machine with the given ID exist.
	/// The given virtual machine and drive are not connected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the connection is added to local data structures and true is returned.
	/// Otherwise, the connection is not added and false is returned.
	/// </remarks>
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

	/// <summary>
	/// Disconnects the given drive from the given virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to disconnect from the virtual machine. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to disconnect the drive from. vmId >= 1.</param>
	/// <returns>True if the connection was removed, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists, and a virtual machine with the given ID exist.
	/// The given virtual machine and drive are connected. driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the connection is removed from local data structures and true is returned.
	/// Otherwise, the connection is not removed and false is returned.
	/// </remarks>
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

	/// <summary>
	/// Handles a virtual machine power off and crash events.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine that was powered off or crashed. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine was powered off or has crashed. vmId >= 1. <br/>
	/// Postcondition: The event is handled, the state of the virtual machine is updated.
	/// </remarks>
	private void OnVmPoweredOffOrCrashed(object? sender, int vmId)
	{
		if (_virtualMachines.TryGetValue(vmId, out VmGeneralDescriptor? vm))
		{
			vm.State = VmState.ShutDown;
		}
	}

	/// <summary>
	/// Handles a virtual machine power on event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine that was powered on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine was powered on. vmId >= 1. <br/>
	/// Postcondition: The event is handled, the state of the virtual machine is updated.
	/// </remarks>
	private void OnVmPoweredOn(object? sender, int vmId)
	{
		if (_virtualMachines.TryGetValue(vmId, out VmGeneralDescriptor? vm))
		{
			vm.State = VmState.Running;
		}	
	}

	/// <summary>
	/// Handles the event that a virtual machine was created.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="descriptor">A general virtual machine descriptor of the new virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine was created. descriptor != null. <br/>
	/// Postcondition: The event is handled, the virtual machine is added.
	/// </remarks>
	private void OnVmCreated(object? sender, VmGeneralDescriptor descriptor) =>
		_virtualMachines.TryAdd(descriptor.Id, descriptor);

	/// <summary>
	/// Handles the event that a virtual machine was deleted.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine that was deleted. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: Service initialized. A virtual machine was deleted. vmId >= 1. <br/>
	/// Postcondition: The event is handled, the virtual machine is removed.
	/// </remarks>
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

	/// <summary>
	/// Handles the event that a new drive was created.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="descriptor">A general drive descriptor of the new drive. descriptor != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. A drive was created. descriptor != null. <br/>
	/// Postcondition: The event is handled, the drive is added.
	/// </remarks>
	private void OnDriveCreated(object? sender, DriveGeneralDescriptor descriptor) =>
		_drives.TryAdd(descriptor.Id, descriptor);

	/// <summary>
	/// Handles the event that an item (either drive or a file in a drive) was deleted.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="info">Information about the deletion event. info != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. An item was deleted. info != null. <br/>
	/// Postcondition: The event is handled, the item is removed.
	/// </remarks>
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

	/// <summary>
	/// Handles the event that a new drive connection was made. (Drive connected to virtual machine)
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="connection">The new connection. connection != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. A new drive connection was made. connection != null. <br/>
	/// Postcondition: Event is handled, the connection is added.
	/// </remarks>
	private void OnDriveConnected(object? sender, DriveConnection connection) =>
		AddConnection(connection.DriveId, connection.VmId);
	
	/// <summary>
	/// Handles the event that a drive connection was removed. (Drive disconnected from virtual machine)
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="connection">The removed connection. connection != null.</param>
	/// <remarks>
	/// Precondition: Service initialized. A drive connection was removed. connection != null. <br/>
	/// Postcondition: Event is handled, the connection is removed.
	/// </remarks>
	private void OnDriveDisconnected(object? sender, DriveConnection connection) =>
		RemoveConnection(connection.DriveId, connection.VmId);
}