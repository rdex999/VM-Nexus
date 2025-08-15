using Client.Services;

namespace Client.ViewModels;

public class CreateVmViewModel : ViewModelBase
{
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}
}