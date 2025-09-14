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
}