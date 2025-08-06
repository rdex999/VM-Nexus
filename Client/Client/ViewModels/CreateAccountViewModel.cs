using System.Threading.Tasks;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private CommunicationService _communicationService;

	public CreateAccountViewModel(NavigationService navigationService,  CommunicationService communicationService)
	{
		_navigationService = navigationService;
		_communicationService = communicationService;
	}

	[RelayCommand]
	private void NavigateToLogin()
	{
		_navigationService.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(_navigationService, _communicationService) });
	}
}