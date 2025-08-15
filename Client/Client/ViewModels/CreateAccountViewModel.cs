using System;
using System.Threading.Tasks;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Networking;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
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
	
	[ObservableProperty]
	private string _accountCreationFailedText;

	[ObservableProperty] 
	private string _usernameAvailabilityMessage = "Username must not be empty.";
	
	[ObservableProperty]
	private bool _usernameAvailabilitySuccessClass = false;
	
	public CreateAccountViewModel(NavigationService navigationSvc,  ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.Reconnected += OnReconnected;
	}

	[RelayCommand]
	private void NavigateToLogin()
	{
		NavigationSvc.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(NavigationSvc, ClientSvc) });
	}

	[RelayCommand]
	private async Task CreateAccountAsync()
	{
		AccountCreationFailedTextIsVisible = false;
		
		MessageResponseCreateAccount.Status status= await ClientSvc.CreateAccountAsync(Username, Password);
		if (status == MessageResponseCreateAccount.Status.Success)
		{
			NavigationSvc.NavigateToView(new MainView() {  DataContext = new MainViewModel(NavigationSvc, ClientSvc, Username) });
		}
		else if  (status == MessageResponseCreateAccount.Status.UsernameNotAvailable)
		{
			AccountCreationFailedText = $"Username \"{Username}\" is not available.";
			AccountCreationFailedTextIsVisible = true;
		}
		else
		{
			AccountCreationFailedText = "Account creation failed. Try again later.";
			AccountCreationFailedTextIsVisible = true;
		}
	}

	public async Task UsernameTextChangedAsync()
	{
		AccountCreationFailedTextIsVisible = false;

		if (!string.IsNullOrEmpty(Username))
		{
			bool usernameAvailable = await ClientSvc.IsUsernameAvailableAsync(Username);
			if (usernameAvailable)
			{
				UsernameAvailabilitySuccessClass = true;
				UsernameAvailabilityMessage = $"Username {Username} is available.";
			}
			else
			{
				UsernameAvailabilitySuccessClass = false;
				UsernameAvailabilityMessage = $"Username {Username} is not available.";
			}
		}
		else
		{
			UsernameAvailabilitySuccessClass = false;
			UsernameAvailabilityMessage = "Username must not be empty.";
		}
		CreateAccountIsEnabledSetup();
	}
	
	public void PasswordTextChanged()
	{
		AccountCreationFailedTextIsVisible = false;
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

			CreateAccountIsEnabledSetup();
		}
		else
		{
			PasswordNotEqualTextIsVisible = true;
			PasswordClassError = true;
			PasswordClassSuccess = false;
			CreateAccountIsEnabled = false;
		}
	}

	private void OnReconnected(object? sender, EventArgs e)
	{
		CreateAccountIsEnabledSetup();
	}

	private void CreateAccountIsEnabledSetup()
	{
		CreateAccountIsEnabled = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password) &&
		                         Password == PasswordConfirm && ClientSvc.IsConnected() 
		                         && UsernameAvailabilitySuccessClass;
	}
}