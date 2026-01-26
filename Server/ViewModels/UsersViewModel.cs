using Server.Services;

namespace Server.ViewModels;

public class UsersViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;

	public UsersViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
	}
}