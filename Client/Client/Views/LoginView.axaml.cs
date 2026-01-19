using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.ViewModels;

namespace Client.Views;

public partial class LoginView : UserControl
{
	public LoginView()
	{
		InitializeComponent();
		this.Loaded += OnLoaded;
	}

	/// <summary>
	/// Handles the Loaded event - initializes LoginViewModel.
	/// </summary>
	/// <remarks>
	/// Precondition: LoginView is loaded. <br/>
	/// Postcondition: LoginViewModel started initialization procedure.
	/// </remarks>
	private void OnLoaded(object? sender, RoutedEventArgs e)
	{
		if (DataContext is not LoginViewModel loginViewModel)
			return;
		
		if (!loginViewModel.IsInitialized())
			_ = loginViewModel.InitializeAsync();
	}

	/// <summary>
	/// Handles changes in the credentials input fields. (username, password)
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: User has typed/deleted from the credentials input fields. <br/>
	/// Postcondition: Change handled - buttons and errors are displayed accordingly. (username empty, etc)
	/// </remarks>
	private void OnCredentialsTextChanged(object? sender, TextChangedEventArgs e)
	{
		if (DataContext is LoginViewModel loginViewModel)
			loginViewModel.OnCredentialsTextChanged();
	}
}