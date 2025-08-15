using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	[ObservableProperty]
	private string _username;
	
	[ObservableProperty]
	private string _password;

	[ObservableProperty] 
	private string _errorMessage;
	
	[ObservableProperty]
	private bool _loginButtonIsEnabled = false;

	public LoginViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.Reconnected += OnReconnected;
	}

	public async Task InitializeAsync()
	{
		if (IsInitialized())
			return;
		
		await ClientSvc.InitializeAsync();
	}

	public bool IsInitialized()
	{
		return ClientSvc.IsInitialized();
	}

	[RelayCommand]
	private void NavigateToCreateAccount()
	{
		NavigationSvc.NavigateToView(new CreateAccountView() { DataContext = new CreateAccountViewModel(NavigationSvc, ClientSvc) });
	}

	[RelayCommand]
	private async Task LoginAsync()
	{
		bool? result = await ClientSvc.LoginAsync(Username, Password);
		if (result == null)
		{
			ErrorMessage = "Server error. Try again later.";
		} 
		else if (result.Value)
		{
			NavigationSvc.NavigateToView(new MainView() { DataContext = new MainViewModel(NavigationSvc, ClientSvc, Username) });
		}
		else
		{
			ErrorMessage = "Incorrect username or password.";
		}
	}

	public void OnCredentialsTextChanged()
	{
		ErrorMessage = string.Empty;
		LoginButtonIsEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && ClientSvc.IsConnected();
	}

	private void OnReconnected(object? sender, EventArgs eventArgs)
	{
		LoginButtonIsEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && ClientSvc.IsConnected();
	}
}