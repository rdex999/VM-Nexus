using Client.Services;

namespace Client.ViewModels;

public class HomeViewModel : ViewModelBase
{
	public HomeViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}