using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Client.Services;

public class NavigationService
{
	public NavigationService(){}

	public void NavigateToView(UserControl view)
	{
		if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow.Content = view;
		}
		else if (Application.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = view;
		}
	}
}