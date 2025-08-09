using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
	protected readonly NavigationService _navigationService;
	protected ClientService _clientService;

	public ViewModelBase(NavigationService navigationService, ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;
	}
}