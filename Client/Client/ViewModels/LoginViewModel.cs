using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private ClientService _clientService;

	[ObservableProperty]
	private string _username;
	
	[ObservableProperty]
	private string _password;

	[ObservableProperty] 
	private string _errorMessage;
	
	[ObservableProperty]
	private bool _loginButtonIsEnabled = false;

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

	[RelayCommand]
	private async Task LoginAsync()
	{
		bool? result = await _clientService.LoginAsync(Username, Password);
		if (result == null)
		{
			ErrorMessage = "Server error. Try again later.";
		} 
		else if (result.Value)
		{
			_navigationService.NavigateToView(new MainView() { DataContext = new MainViewModel(_navigationService, _clientService) });
		}
		else
		{
			ErrorMessage = "Incorrect username or password.";
		}
	}

	public void OnCredentialsTextChanged()
	{
		ErrorMessage = string.Empty;
		LoginButtonIsEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
	}
}