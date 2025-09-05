using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Client.Services;
using Client.ViewModels;
using Client.Views;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;

namespace Client;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			#if DEBUG
				this.AttachDevTools();
			#endif
		}
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			// Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
			// More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
			DisableAvaloniaDataAnnotationValidation();

			ClientService clientService = new ClientService();
			desktop.MainWindow = new MainWindow
			{
				Content = new LoginView() { DataContext = new LoginViewModel(new NavigationService(), clientService) }
			};
			desktop.Exit += (s, e) => clientService.OnExit();
		}
		else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = new LoginView()
			{
				DataContext = new LoginViewModel(new NavigationService(), new ClientService())
			};
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void DisableAvaloniaDataAnnotationValidation()
	{
		// Get an array of plugins to remove
		var dataValidationPluginsToRemove =
			BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

		// remove each entry found
		foreach (var plugin in dataValidationPluginsToRemove)
		{
			BindingPlugins.DataValidators.Remove(plugin);
		}
	}
}