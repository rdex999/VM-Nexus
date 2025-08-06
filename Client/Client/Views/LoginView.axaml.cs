using System.Threading.Tasks;
using Avalonia.Controls;
using Client.ViewModels;

namespace Client.Views;

public partial class LoginView : UserControl
{
	public LoginView()
	{
		InitializeComponent();
		this.Loaded += async (_, _) => await OnLoadedAsync();
	}

	private async Task OnLoadedAsync()
	{
		if (DataContext is LoginViewModel loginViewModel)
		{
			if (!loginViewModel.IsInitialized())
			{
				await loginViewModel.Initialize();
			}
		}
	}
}