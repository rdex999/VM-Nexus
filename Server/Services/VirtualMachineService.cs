using System.Collections.Concurrent;
using System.Threading.Tasks;
using Shared;

namespace Server.Services;

public class VirtualMachineService
{
	private string _username = string.Empty;
	private DatabaseService _databaseService;
	private ConcurrentDictionary<string, VirtualMachine> _runningVirtualMachines;

	public VirtualMachineService(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		_runningVirtualMachines = new ConcurrentDictionary<string, VirtualMachine>();
	}

	public async Task<ExitCode> LoginAsync(string username)
	{
		if (IsLoggedIn())
		{
			await LogoutAsync();
		}
		
		_username = username;
		
		/* TODO: Fetch running virtual machines from database */
		return ExitCode.Success;
	}

	public async Task LogoutAsync(int keepRunningMinutes = 0)
	{
		if (!IsLoggedIn())
		{
			return;
		}
		
		/* TODO: Wait for keepRunningMinutes minutes and then shut down the virtual machines */
		_username = string.Empty;
		_runningVirtualMachines.Clear();
	}

	private bool IsLoggedIn() => !string.IsNullOrEmpty(_username);

	private class VirtualMachine
	{
	}
}

