using Client.Services;

namespace Client.ViewModels;

public partial class MainViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private ClientService _clientService;

	public MainViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}