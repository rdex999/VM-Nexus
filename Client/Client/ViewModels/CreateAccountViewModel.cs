using System.Threading.Tasks;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;

	public CreateAccountViewModel(NavigationService navigationService)
	{
		_navigationService = navigationService;
	}

	[RelayCommand]
	private void NavigateToLogin()
	{
		_navigationService.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(_navigationService) });
	}
}