using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;

	public LoginViewModel(NavigationService navigationService)
	{
		_navigationService = navigationService;
	}

	[RelayCommand]
	private void NavigateToCreateAccount()
	{
		_navigationService.NavigateToView(new CreateAccountView() { DataContext = new CreateAccountViewModel(_navigationService) });
	}
}