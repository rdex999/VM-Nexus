using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;
using Shared.Networking;


namespace Client.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
	[ObservableProperty]
	private string _username = string.Empty;
	
	[ObservableProperty]
	private string _password  = string.Empty;

	[ObservableProperty] 
	private string _errorMessage  = string.Empty;
	
	[ObservableProperty]
	private bool _loginButtonIsEnabled = false;

	/// <summary>
	/// Initializes a new instance of LoginViewModel.
	/// </summary>
	/// <param name="navigationSvc">
	/// The navigation service. navigationSvc != null.
	/// </param>
	/// <param name="clientSvc">
	/// The client service. clientSvc != null.
	/// </param>
	/// <remarks>
	/// Precondition: App loaded (this is the initial screen) or user redirected to the login page. <br/>
	/// navigationSvc != null &amp;&amp; clientSvc != null. <br/>
	/// Postcondition: A new instance of LoginViewModel is created.
	/// </remarks>
	public LoginViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.Reconnected += OnReconnected;
		ClientSvc.FailEvent += OnFailure;
	}

	/// <summary>
	/// Checks whether this view model is initialized or not.
	/// </summary>
	/// <returns>
	/// True if initialized already, false otherwise.
	/// </returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns whether this view model is initialized or not.
	/// </remarks>
	public bool IsInitialized()
	{
		return ClientSvc.IsInitialized();
	}

	/// <summary>
	/// Navigates to the create account page.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the create account button. <br/>
	/// Postcondition: The create account page is now the current page - its what the user sees now.
	/// </remarks>
	[RelayCommand]
	private void NavigateToCreateAccount() => NavigationSvc.NavigateToCreateAccount();

	/// <summary>
	/// Tries to log in with the inputted username and password. Occurs when the user clicks on the login button.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the login button. The username and password fields are not empty. Username != null &amp;&amp; Password != null. <br/>
	/// Postcondition: On successful login, the user is redirected to the main view. On failure, an error message is displayed and the user stays on the login page.
	/// </remarks>
	[RelayCommand]
	private async Task LoginAsync()
	{
		if (!LoginButtonIsEnabled)
			return;
		
		MessageResponseLogin? res = await ClientSvc.LoginAsync(Username, Password);
		
		if (res == null)
			ErrorMessage = "Server error. Try again later.";
	
		else if (res.Result == MessageResponseLogin.Status.Success)
		{
			NavigationSvc.NavigateToMainPage();
			Username = string.Empty;
			Password = string.Empty;
		}
		
		else if (res.Result == MessageResponseLogin.Status.Failure)
			ErrorMessage = "Invalid username or password.";
		
		else
			ErrorMessage = $"Too many failed login attempts. Try again in {double.Ceiling(res.LoginBlock.TotalMinutes)} minutes.";
	}

	/// <summary>
	/// Handles a change in the login credentials.
	/// </summary>
	/// <remarks>
	/// Precondition: User has typed/deleted something from the username or password input fields. <br/>
	/// Postcondition: The login button is enabled if the credentials are valid, (not empty or null)
	/// and if there was a login error message, (from previous login attempts) its gone.
	/// </remarks>
	public void OnCredentialsTextChanged()
	{
		ErrorMessage = string.Empty;
		LoginButtonIsEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && ClientSvc.IsConnected();
	}

	/// <summary>
	/// Handles the reconnection event from the client service.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="eventArgs"></param>
	/// <remarks>
	/// Precondition: The client service has connected to the server. <br/>
	/// Postcondition: If the credentials input fields are valid, the login button is enabled, if the credentials are invalid, the login button is disabled.
	/// </remarks>
	private void OnReconnected(object? sender, EventArgs eventArgs)
	{
		LoginButtonIsEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && ClientSvc.IsConnected();
	}

	/// <summary>
	/// Handles' failures. For example, if the client service is suddenly disconnected from the server.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="status">
	/// The exit code - indicates the type of failure.
	/// </param>
	/// <remarks>
	/// Precondition: Failure of some kind has happened. The status parameter should hold the type of failure. <br/>
	/// Postcondition: Error messages are displayed as needed. If disconnected from the server, the login button is disabled.
	/// </remarks>
	private void OnFailure(object? sender, ExitCode status)
	{
		switch (status)
		{
			case ExitCode.DisconnectedFromServer:
			{
				LoginButtonIsEnabled = false;
				break;
			}
		}
	}
}