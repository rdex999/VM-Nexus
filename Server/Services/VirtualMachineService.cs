using System.Collections.Concurrent;
using System.Threading.Tasks;
using Server.VirtualMachines;
using Shared;

namespace Server.Services;

public class VirtualMachineService
{
	private DatabaseService _databaseService;
	
	/* By usernames, then by VM names */
	private ConcurrentDictionary<string, ConcurrentDictionary<string, VirtualMachine>> _runningVirtualMachines;

	public VirtualMachineService(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		_runningVirtualMachines = new ConcurrentDictionary<string, ConcurrentDictionary<string, VirtualMachine>>();
	}

	/// <summary>
	/// Get the state of a virtual machine.
	/// </summary>
	/// <param name="username">The username of the user that owns the virtual machine. username != null</param>
	/// <param name="vmName">The name of the virtual machine to check the state of. vmName != null.</param>
	/// <returns>The state of the virtual machine, or -1 on failure.</returns>
	/// <remarks>
	/// Precondition: A user exists with the given username. The user has a virtual machine with the given name. <br/>
	/// username != null &amp;&amp; vmName != null <br/>
	/// Postcondition: On success, the state of the virtual machine is returned. On failure, -1 is returned.
	/// </remarks>
	public async Task<SharedDefinitions.VmState> GetVmStateAsync(string username, string vmName)
	{
		SharedDefinitions.VmState vmState = await _databaseService.GetVmStateAsync(username, vmName);
		return vmState;
	}
	
	/// <summary>
	/// Creates a virtual machine in the database.
	/// </summary>
	/// <param name="username">The username of the owner user of the virtual machine. username != null.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the virtual machine.</param>
	/// <param name="cpuArchitecture">The CPU architecture (x86, x86-64, etc..) of the virtual machine.</param>
	/// <param name="bootMode">The boot mode for the virtual machine. (UEFI or BIOS)</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: A user with the given username must exist,
	/// there should not be a virtual machine with the given name under this user. (name is unique).
	/// username != null &amp;&amp; name != null. <br/>
	/// Postcondition: On success, a virtual machine with the given parameters is created. On failure, the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> CreateVirtualMachineAsync(string username, string name,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.CpuArchitecture cpuArchitecture,
		SharedDefinitions.BootMode bootMode)
	{
		return await _databaseService.CreateVmAsync(username, name, operatingSystem, cpuArchitecture, bootMode);
	}

	/// <summary>
	/// Checks if a virtual machine with the given name exists under a user with the given username.
	/// </summary>
	/// <param name="username">The username of the user to search for the VM under. username != null.</param>
	/// <param name="name">The name of the virtual machine. name != null.</param>
	/// <returns>True if the virtual machine exists, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A user with the given username must exist. username != null &amp;&amp; name != null. <br/>
	/// Postcondition: Returns true if the virtual machine exists, false if the virtual machine does not exist or on failure.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(string username, string name)
	{
		return await _databaseService.IsVmExistsAsync(username, name);
	}
}