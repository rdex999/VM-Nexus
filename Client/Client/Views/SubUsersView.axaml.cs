using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Client.Views;

public partial class SubUsersView : UserControl
{
	public SubUsersView()
	{
		InitializeComponent();
	}

	private async void OnNewSubUserPopupUsernameChangedAsync(object? sender, TextChangedEventArgs e)
	{
	}

	private void OnNewSubUserPopupEmailChanged(object? sender, TextChangedEventArgs e)
	{
	}

	private void OnNewSubUserPopupPasswordChanged(object? sender, TextChangedEventArgs e)
	{
	}

	private void OnNewSubUserPopupPasswordConfirmLostFocus(object? sender, RoutedEventArgs e)
	{
	}
}