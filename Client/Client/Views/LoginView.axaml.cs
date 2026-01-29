using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.ViewModels;

namespace Client.Views;

public partial class LoginView : UserControl
{
	public LoginView()
	{
		InitializeComponent();
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