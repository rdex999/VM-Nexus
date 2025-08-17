using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Client.Services;

public class NavigationService
{

	/// <summary>
	/// Navigates to the given view.
	/// </summary>
	/// <param name="view">
	/// The view to navigate to.
	/// </param>
	/// <remarks>
	/// Precondition: Application loaded and the MainWindow was created. view != null. <br/>
	/// Postcondition: The given view is set as the current view. Meaning, the user now sees the given page. (view)
	/// </remarks>
	public void NavigateToView(UserControl view)
	{
		if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow!.Content = view;
		}
		else if (Application.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = view;
		}
	}
}