using System.Collections.Concurrent;
using System.Collections.Generic;
using Shared;

namespace Client.Services;

public class DriveService
{
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

	private bool AddConnection(int driveId, int vmId)
	{
		if (ConnectionExists(driveId, vmId)) return false;

		if (!_drivesByVmId.TryGetValue(driveId, out HashSet<int>? drives))
		{
			_drivesByVmId[vmId] = new HashSet<int>();
		}
		drives!.Add(vmId);

		if (!_vmsByDriveId.TryGetValue(driveId, out HashSet<int>? vms))
		{
			_vmsByDriveId[driveId] = new HashSet<int>();
		}
		vms!.Add(vmId);
		
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