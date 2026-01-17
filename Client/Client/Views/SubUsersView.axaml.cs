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

	private void OnNewSubUserPopupPasswordChanged(object? sender, TextChangedEventArgs e)
	{
	}

	private void OnNewSubUserPopupPasswordConfirmLostFocus(object? sender, RoutedEventArgs e)
	{
	}
}