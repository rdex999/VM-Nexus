using System;
using System.Threading.Tasks;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	private readonly NavigationService _navigationService;
	private readonly ClientService _clientService;

	[ObservableProperty] 
	private string _username;
	
	[ObservableProperty]
	private string _password;
	
	[ObservableProperty]
	private string _passwordConfirm;

	[ObservableProperty]
	private bool _passwordNotEqualTextIsVisible = false;

	[ObservableProperty] 
	private bool _passwordClassError = false;

	[ObservableProperty] 
	private bool _passwordClassSuccess = false;
	
	[ObservableProperty]
	private bool _createAccountIsEnabled = false;
	
	[ObservableProperty]
	private bool _accountCreationFailedTextIsVisible = false;
	
	public CreateAccountViewModel(NavigationService navigationService,  ClientService clientService)
	{
		_navigationService = navigationService;
		_clientService = clientService;
	}


	[RelayCommand]
	private void NavigateToLogin()
	{
		_navigationService.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(_navigationService, _clientService) });
	}

	[RelayCommand]
	private async Task CreateAccountAsync()
	{
		bool success = await _clientService.CreateAccountAsync(Username, Password);
		if (success)
		{
			AccountCreationFailedTextIsVisible = false;		/* User should not even see that disappear, but in case he does. */
			_navigationService.NavigateToView(new MainView() {  DataContext = new MainViewModel(_navigationService, _clientService) });
		}
		else
		{
			AccountCreationFailedTextIsVisible = true;
		}
	}

	public async Task UsernameTextChangedAsync()
	{
		CreateAccountIsEnabled = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password) &&
		    !string.IsNullOrEmpty(PasswordConfirm) && Password == PasswordConfirm && _clientService.IsConnected();
		
		/* TODO: Search database for the username, to display a message if the username is available or not */
	}
	
	public void PasswordTextChanged()
	{
		if (Password == PasswordConfirm && Password.Length == 0)
		{
			PasswordNotEqualTextIsVisible = false;
			PasswordClassError = false;	
			PasswordClassSuccess = false;
			CreateAccountIsEnabled = false;
		}
		else if (Password == PasswordConfirm)
		{
			PasswordNotEqualTextIsVisible = false;
			PasswordClassError = false;
			PasswordClassSuccess = true;

			CreateAccountIsEnabled = !string.IsNullOrEmpty(Username);
		}
		else
		{
			PasswordNotEqualTextIsVisible = true;
			PasswordClassError = true;
			PasswordClassSuccess = false;
			CreateAccountIsEnabled = false;
		}
	}
}