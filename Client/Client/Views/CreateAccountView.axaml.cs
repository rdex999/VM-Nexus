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

	private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
	{
		((CreateAccountViewModel)DataContext!).PasswordTextChanged();
	}

	private async void OnUsernameTextChangedAsync(object? sender, TextChangedEventArgs e)
	{
		await ((CreateAccountViewModel)DataContext!).UsernameTextChangedAsync();
	}

	private void PasswordConfirmTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
	{
		PasswordStateButton.IsChecked = false;
	}
}