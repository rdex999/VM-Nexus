using System.Threading.Tasks;
using Server.Services;
using Shared;

namespace Server.ViewModels;

public class VirtualMachinesViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;

	public VirtualMachinesViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		_ = RefreshAsync();
	}

	/// <summary>
	/// Refreshes the current virtual machines list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the virtual machines list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	public async Task<ExitCode> RefreshAsync()
	{
		return ExitCode.Success;
	}
}