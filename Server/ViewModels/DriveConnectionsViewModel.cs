using System.Threading.Tasks;
using Server.Services;
using Shared;

namespace Server.ViewModels;

public class DriveConnectionsViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;
	
	public DriveConnectionsViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
	}

	/* Use for IDE preview only. */
	public DriveConnectionsViewModel()
	{
		_databaseService = null!;
	}
	
	/// <summary>
	/// Refreshes the current drive connections list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the drive connections list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	public async Task<ExitCode> RefreshAsync()
	{
		return ExitCode.Success;
	}
}