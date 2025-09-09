using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Services;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

namespace Client.ViewModels;

public partial class CreateAccountViewModel : ViewModelBase
{
	[ObservableProperty] 
	private string _username = string.Empty;
	
	[ObservableProperty]
	private string _password = string.Empty;
	
	[ObservableProperty]
	private string _passwordConfirm  = string.Empty;

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
	private string _accountCreationFailedText = string.Empty;

	[ObservableProperty] 
	private string _usernameAvailabilityMessage = "Username must not be empty.";
	
	[ObservableProperty]
	private bool _usernameAvailabilitySuccessClass = false;
	
	[ObservableProperty]
	private bool _usernameAvailabilityErrorClass = true;

	[ObservableProperty] 
	private string _email = string.Empty;
	
	[ObservableProperty] 
	private bool _emailSuccessClass = false;
	
	[ObservableProperty]
	private string _emailErrorMessage = string.Empty;

	/// <summary>
	/// Initializes a new instance of CreateAccountViewModel.
	/// </summary>
	/// <param name="navigationSvc">
	/// the navigation service. navigationSvc != null.
	/// </param>
	/// <param name="clientSvc">
	/// The client service. clientSvc != null
	/// </param>
	/// <remarks>
	/// Precondition: User has clicked on the create account button in the login page. navigationSvc != null &amp;&amp; clientSvc != null. <br/>
	/// Postcondition: A new instance of CreateAccountViewModel is created.
	/// </remarks>
	public CreateAccountViewModel(NavigationService navigationSvc,  ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.Reconnected += OnReconnected;
		ClientSvc.FailEvent += OnFailure;
	}

	/// <summary>
	/// Navigates to the login page. (view)
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the login button. <br/>
	/// Postcondition: User is redirected to the login view.
	/// </remarks>
	[RelayCommand]
	private void NavigateToLogin()
	{
		NavigationSvc.NavigateToView(new LoginView() {  DataContext = new LoginViewModel(NavigationSvc, ClientSvc) });
	}
	
	/// <summary>
	/// Tries to create an account with the inputted username and password.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the create account button,
	/// credentials should be valid, (Password==PasswordConfirm, Email valid, no empty credentials field) Client service connected to the server. <br/>
	/// Postcondition: On success, the user is redirected to the main page and is logged in. <br/>
	/// On failure, a corresponding error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task CreateAccountAsync()
	{
		AccountCreationFailedTextIsVisible = false;

		string usernameTrimmed = Username.Trim();
		
		MessageResponseCreateAccount.Status status= await ClientSvc.CreateAccountAsync(usernameTrimmed, Email.Trim(), Password);
		switch (status)
		{
			case MessageResponseCreateAccount.Status.Success:
			{
				NavigationSvc.NavigateToView(new MainView() {  DataContext = new MainViewModel(NavigationSvc, ClientSvc, usernameTrimmed) });
				break;
			}
			case MessageResponseCreateAccount.Status.CredentialsCannotBeEmpty:
			{
				AccountCreationFailedText = "Credentials cannot be empty.";
				AccountCreationFailedTextIsVisible = true;
				break;
			}
			case MessageResponseCreateAccount.Status.UsernameNotAvailable:
			{
				AccountCreationFailedText = $"Username \"{usernameTrimmed}\" is not available.";
				AccountCreationFailedTextIsVisible = true;
				break;
			}
			case MessageResponseCreateAccount.Status.Failure:
			{
				AccountCreationFailedText = "Account creation failed. Try again later.";
				AccountCreationFailedTextIsVisible = true;
				break;
			}
		}
	}

	/// <summary>
	/// Handles a change in the username input field.
	/// </summary>
	/// <remarks>
	/// Precondition: Validation of the inputted username is needed - after the username field changed, server reconnection, etc. <br/>
	/// Postcondition: If the username is valid, then an according message is displayed, if invalid, then an according error message is displayed.
	/// </remarks>
	public async Task ValidateUsernameAsync()
	{
		AccountCreationFailedTextIsVisible = false;

		if (string.IsNullOrEmpty(Username))
		{
			UsernameAvailabilitySuccessClass = false;
			UsernameAvailabilityErrorClass = true;
			UsernameAvailabilityMessage = "Username must not be empty.";
			CreateAccountIsEnabledSetup();
			return;
		}

		string usernameTrimmed = Username.Trim();
		
		if (!Common.IsValidUsername(usernameTrimmed))
		{
			UsernameAvailabilityErrorClass = true;
			UsernameAvailabilitySuccessClass = false;
			
			string invalidChars = string.Empty;
			for(int i = 0; i < SharedDefinitions.InvalidUsernameCharacters.Length; i++)
			{
				invalidChars += SharedDefinitions.InvalidUsernameCharacters[i];
				if (i == SharedDefinitions.InvalidUsernameCharacters.Length - 1)
				{
					invalidChars += '.';
				}
				else
				{
					invalidChars += ", ";
				}
			}
			UsernameAvailabilityMessage = "Username cannot contain: " + invalidChars;
		}
		else
		{
			UsernameAvailabilitySuccessClass = false;
			UsernameAvailabilityErrorClass = false;
			UsernameAvailabilityMessage = string.Empty;

			/*
			 * To avoid delays because of disconnections. On reconnection, IsUsernameAvailable is called from OnReconnected().
			 * Calling IsUsernameAvailable from different threads seems to cause problems.
			 */
			bool? usernameAvailable = null;
			if (ClientSvc.IsConnected())	
			{
				usernameAvailable = await ClientSvc.IsUsernameAvailableAsync(usernameTrimmed);
			}
			
			if (usernameAvailable.HasValue && usernameAvailable.Value)
			{
				UsernameAvailabilitySuccessClass = true;
				UsernameAvailabilityErrorClass = false;
				UsernameAvailabilityMessage = $"Username {usernameTrimmed} is available.";
			}
			else if(usernameAvailable.HasValue && !usernameAvailable.Value)
			{
				UsernameAvailabilitySuccessClass = false;
				UsernameAvailabilityErrorClass = true;
				UsernameAvailabilityMessage = $"Username {usernameTrimmed} is not available.";
			}
		}

		CreateAccountIsEnabledSetup();
	}

	/// <summary>
	/// Handles a change in the password input field.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has typed/deleted something from the password input field. <br/>
	/// Postcondition: Success/Error messages are displayed as needed, the create account button is enabled if all inputted credentials meet the conditions.
	/// </remarks>
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

	/// <summary>
	/// Handles a change in the email input field.
	/// </summary>
	/// <remarks>
	/// Precondition: User has typed/deleted something from the email input field. <br/>
	/// Postcondition: Error messages are displayed if needed, (if the email is invalid or empty) UI styles are updated if needed.
	/// </remarks>
	public void OnEmailTextChanged()
	{
		if (string.IsNullOrEmpty(Email))
		{
			EmailSuccessClass = false;
			EmailErrorMessage = "Email must not be empty.";
			return;
		}

		if (Common.IsValidEmail(Email))
		{
			EmailSuccessClass = true;
			EmailErrorMessage = string.Empty;
		}
		else
		{
			EmailSuccessClass = false;
			EmailErrorMessage = "Invalid email address.";
		}
	}
	
	/// <summary>
	/// Handles the reconnected event from the client service. <br/>
	/// When was disconnected, the username might have changed, so check if its available.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: Client service has connected to the server. <br/>
	/// Postcondition: After the asynchronous task is completed, (its a fire and forget) an according success/error
	/// message will appear on the username input field, and the create account button will be enabled if all credentials meet the conditions.
	/// </remarks>
	private void OnReconnected(object? sender, EventArgs e)
	{
		Dispatcher.UIThread.Post(async void () =>
		{
			await ValidateUsernameAsync();
			CreateAccountIsEnabledSetup();
		});
	}

	/// <summary>
	/// Handles failures.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="status">
	/// Indicates the type of failure.
	/// </param>
	/// <remarks>
	/// Precondition: A failure of some kind has happened. status indicates the type of failure. <br/>
	/// Postcondition: The failure is considered as handled. The UI will change according to the failure. (Error messages, etc)
	/// </remarks>
	private void OnFailure(object? sender, ExitCode status)
	{
		switch (status)
		{
			case ExitCode.DisconnectedFromServer:
			{
				CreateAccountIsEnabled = false;
				break;
			}
		}
	}

	/// <summary>
	/// Enables/Disables the create account button based on the condition of all input fields.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: The create account button will be enabled if all conditions are met: <br/>
	/// - 1: Username, Password, PasswordConfirm are not null or empty. <br/>
	/// - 2: Password == PasswordConfirm. <br/>
	/// - 3: The client service is connected to the server. <br/>
	/// - 4: The inputted username is available. (there is no user with that username)
	/// </remarks>
	private void CreateAccountIsEnabledSetup()
	{
		CreateAccountIsEnabled = !string.IsNullOrEmpty(Username) && Common.IsValidUsername(Username) && !string.IsNullOrEmpty(Password) &&
		                         Password == PasswordConfirm && ClientSvc.IsConnected() 
		                         && UsernameAvailabilitySuccessClass;
	}
}