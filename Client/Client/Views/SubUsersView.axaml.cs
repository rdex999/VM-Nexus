using Avalonia.Controls;
using Avalonia.Interactivity;
using Client.ViewModels;

namespace Client.Views;

public partial class SubUsersView : UserControl
{
	public SubUsersView()
	{
		InitializeComponent();
	}

	/// <summary>
	/// Handles a change in the username field of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the username field. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	private async void OnNewSubUserPopupUsernameChangedAsync(object? sender, TextChangedEventArgs e)
	{
		if (DataContext is SubUsersViewModel subUsersViewModel)
			await subUsersViewModel.OnNewSubUserPopupUsernameChangedAsync();
	}

	/// <summary>
	/// Handles a change in the email field of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the email field. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	private void OnNewSubUserPopupEmailChanged(object? sender, TextChangedEventArgs e)
	{
		if (DataContext is SubUsersViewModel subUsersViewModel)
			subUsersViewModel.OnNewSubUserPopupEmailChanged();
	}

	/// <summary>
	/// Handles a change in the both password and password confirmation fields of the sub-user creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has changed the value of the password or password confirm fields. <br/>
	/// Postcondition: Errors and messages are displayed as needed. The create sub-user button is enabled if everything is valid.
	/// </remarks>
	private void OnNewSubUserPopupPasswordChanged(object? sender, TextChangedEventArgs e)
	{
		if (DataContext is SubUsersViewModel subUsersViewModel)
			subUsersViewModel.OnNewSubUserPopupPasswordChanged();
	}

	/// <summary>
	/// Handles lost focus event in the password confirm input field. This is done to untoggle the reveal password button.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: The password confirm input field has lost focus. <br/>
	/// Postcondition: The reveal password toggle button is untoggled.
	/// </remarks>
	private void OnNewSubUserPopupPasswordConfirmLostFocus(object? sender, RoutedEventArgs e) =>
		PasswordStateButton.IsChecked = false;
}