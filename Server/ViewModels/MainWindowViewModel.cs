using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Models;
using Shared;

namespace Server.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly MainWindowModel _mainWindowModel;
	
	[ObservableProperty]
	private bool _serverStateIsChecked;
	
	public MainWindowViewModel()
	{
		_mainWindowModel = new MainWindowModel();
	}

	public void OnProgramExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
	{
		if (ServerStateIsChecked)
		{
			_mainWindowModel.ServerStopAsync().Wait();
		}
	}
		
	[RelayCommand]
	private async Task ServerStateToggleChangedAsync(bool isToggled)
	{
		if (isToggled)
		{
			ExitCode result = await _mainWindowModel.ServerStartAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = false;
			}
		}
		else
		{
			ExitCode result = await _mainWindowModel.ServerStopAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = true;
			}
		}
	}
}