using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private ClientService _clientService;

	public LoginViewModel(NavigationService navigationService, ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;
	}

	public async Task InitializeAsync()
	{
		if (IsInitialized())
			return;
		
		await _clientService.InitializeAsync();
	}

	public bool IsInitialized()
	{
		return _clientService.IsInitialized();
	}

	[RelayCommand]
	private void NavigateToCreateAccount()
	{
		_navigationService.NavigateToView(new CreateAccountView() { DataContext = new CreateAccountViewModel(_navigationService, _clientService) });
	}
}