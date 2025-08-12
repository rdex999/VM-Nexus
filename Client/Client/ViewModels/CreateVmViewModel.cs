using Client.Services;

namespace Client.ViewModels;

public class CreateVmViewModel : ViewModelBase
{
	public CreateVmViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}