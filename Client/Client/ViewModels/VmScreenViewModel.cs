using Client.Services;

namespace Client.ViewModels;

public class VmScreenViewModel : ViewModelBase
{
	public VmScreenViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}
}