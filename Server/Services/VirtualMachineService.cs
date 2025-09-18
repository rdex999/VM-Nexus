using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using libvirt;
using Server.Drives;
using Server.VirtualMachines;
using Shared;

namespace Server.Services;

public class VirtualMachineService
{
	private readonly DatabaseService _databaseService;
	private readonly DriveService _driveService;
	
	/* By virtual machine ID's */
	private ConcurrentDictionary<int, VirtualMachine> _aliveVirtualMachines;
	private Connect _libvirtConnection;

	public VirtualMachineService(DatabaseService databaseService, DriveService driveService)
	{
		_databaseService = databaseService;
		_driveService = driveService;
		_aliveVirtualMachines = new ConcurrentDictionary<int, VirtualMachine>();
		_libvirtConnection = new Connect("qemu:///system");
		_libvirtConnection.Open();
	}

	public void Close()
	{
		_libvirtConnection.Close();
	}

	/// <summary>
	/// Get the state of a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine. id >= 1.</param>
	/// <returns>The state of the virtual machine, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine with the given ID. id >= 1.<br/>
	/// Postcondition: On success, the state of the virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<SharedDefinitions.VmState> GetVmStateAsync(int id)
	{ 
		return await _databaseService.GetVmStateAsync(id);
	}
	
	/// <summary>
	/// Creates a virtual machine in the database.
	/// </summary>
	/// <param name="userId">The ID of the owner user of the virtual machine. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the virtual machine.</param>
	/// <param name="cpuArchitecture">The CPU architecture (x86, x86-64, etc..) of the virtual machine.</param>
	/// <param name="bootMode">The boot mode for the virtual machine. (UEFI or BIOS)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist,
	/// there should not be a virtual machine with the given name under this user. (name is unique).
	/// userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: On success, a virtual machine with the given parameters is created. On failure, the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> CreateVirtualMachineAsync(int userId, string name,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.CpuArchitecture cpuArchitecture,
		SharedDefinitions.BootMode bootMode)
	{
		return await _databaseService.CreateVmAsync(userId, name, operatingSystem, cpuArchitecture, bootMode);
	}

	/// <summary>
	/// Checks if a virtual machine with the given name exists under a user with the given username.
	/// </summary>
	/// <param name="userId">The ID of the user to search for the VM under. userId >= 1.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <returns>True if the virtual machine exists, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A user with the given ID must exist. userId >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: Returns true if the virtual machine exists, false if the virtual machine does not exist or on failure.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(int userId, string name)
	{
		return await _databaseService.IsVmExistsAsync(userId, name);
	}

	public async Task<ExitCode> PowerOnVirtualMachineAsync(int vmId)
	{
		if (vmId < 1)
		{
			return ExitCode.InvalidParameter;
		}
		
		Task<VirtualMachineDescriptor?> vmDescriptor = _databaseService.GetVmDescriptorAsync(vmId);
		Task<DriveDescriptor[]?> vmDriveDescriptors = _databaseService.GetVmDriveDescriptorsAsync(vmId);
		await Task.WhenAll(vmDescriptor, vmDriveDescriptors);

		if (vmDescriptor.Result == null || vmDriveDescriptors.Result == null)
		{
			return ExitCode.VmDoesntExist;
		}
		
		/* TODO: If the VM is sleeping, wake it up. */
		if (vmDescriptor.Result.VmState == SharedDefinitions.VmState.Running)
		{
			return ExitCode.VmAlreadyRunning;
		}
		
		VirtualMachine virtualMachine = new VirtualMachine(_databaseService, _driveService, vmId, vmDescriptor.Result.OperatingSystem,
			vmDescriptor.Result.CpuArchitecture, vmDescriptor.Result.BootMode, vmDriveDescriptors.Result);

		bool addSuccess = false;
		try
		{
			addSuccess = _aliveVirtualMachines.TryAdd(vmId, virtualMachine);
		}
		catch (Exception)
		{
			// ignored
		}

		if (!addSuccess)
		{
			return ExitCode.TooManyVmsRunning;	
		}
		
		ExitCode result = await virtualMachine.PowerOnAsync(_libvirtConnection);
		if(result != ExitCode.Success)
		{
			_aliveVirtualMachines.TryRemove(vmId, out _);
			return result;
		}
		
		return ExitCode.Success;
	}
}