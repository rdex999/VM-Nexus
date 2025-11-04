using Client.Services;

namespace Client.ViewModels.DriveExplorerModes;

public class DrivesViewModel : ViewModelBase
{
	public DrivesViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}