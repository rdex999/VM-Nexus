using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.ViewModels;

namespace Client.Views;

public partial class CreateAccountView : UserControl
{
	public CreateAccountView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles a change in password and password confirm input fields.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: User has deleted/typed something in password or password confirm input fields. <br/>
	/// Postcondition: Errors and buttons are displayed as necessary. (For example, the "create account" button will not be visible if the passwords are not equal.)
	/// </remarks>
	private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
	{
		((CreateAccountViewModel)DataContext!).PasswordTextChanged();
	}

	/// <summary>
	/// Handles a change in the username input field.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: User has deleted/typed something in the username input field. <br/>
	/// Postcondition: Errors and buttons are displayed as necessary. (For example, if there is a user with the inputted username, an error is displayed)
	/// </remarks>
	private async void OnUsernameTextChangedAsync(object? sender, TextChangedEventArgs e)
	{
		await ((CreateAccountViewModel)DataContext!).ValidateUsernameAsync();
	}

	/// <summary>
	/// Handles lost focus event in the password confirm input field. This is done to untoggle the reveal password button.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="e"></param>
	/// <remarks>
	/// Precondition: The password confirm input field has lost focus. <br/>
	/// Postcondition: The reveal password toggle button is untoggled.
	/// </remarks>
	private void PasswordConfirmTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
	{
		PasswordStateButton.IsChecked = false;
	}

	private void OnEmailTextChanged(object? sender, TextChangedEventArgs e)
	{
		((CreateAccountViewModel)DataContext!).OnEmailTextChanged();
	}
}