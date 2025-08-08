using Client.Services;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private ClientService _clientService;

	public MainViewModel(NavigationService navigationService, ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;
	}
}