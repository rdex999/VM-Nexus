using System.Threading.Tasks;
using Server.Services;

namespace Server.ViewModels;

public class DrivesViewModel : ViewModelBase
{
	private DatabaseService _databaseService;

	public DrivesViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		_ = RefreshAsync();
	}

	/* Use for IDE preview only. */
	public DrivesViewModel()
	{
		_databaseService = null!;
	}
	
	/// <summary>
	/// Refreshes the current drives list according to the set query. Handles a click on the refresh button too.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Either the user has clicked on the refresh button, or refreshing the drives list is needed. <br/>
	/// Postcondition: On success, the list is updated according to latest data. On failure, the list is cleared.
	/// </remarks>
	public async Task RefreshAsync()
	{
	}
}