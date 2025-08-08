using System.Threading.Tasks;
using Avalonia.Controls;
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
}