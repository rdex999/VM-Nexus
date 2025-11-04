using Client.Services;

namespace Client.ViewModels;

public class DriveExplorerViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	public DriveExplorerViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
	}
}