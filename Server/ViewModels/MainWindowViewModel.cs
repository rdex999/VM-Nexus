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

	/// <summary>
	/// Creates an instance of MainWindowViewModel.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: An instance of MainWindowViewModel is returned.
	/// </remarks>
	public MainWindowViewModel()
	{
		_mainWindowModel = new MainWindowModel();
	}

	/// <summary>
	/// Handles the program exit event.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="args"></param>
	/// <remarks>
	/// Precondition: Application exit has been requested. (user quite the application) <br/>
	/// Postcondition: Server shut down, application not running.
	/// </remarks>
	public void OnProgramExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
	{
		if (ServerStateIsChecked)
		{
			_mainWindowModel.ServerStop();
		}
	}
	
	/// <summary>
	/// Handles a toggle of the server on/off button.
	/// </summary>
	/// <param name="isToggled">
	/// Indicates whether the toggle event is for toggling on or off.
	/// </param>
	/// <remarks>
	/// Precondition: User has toggled the server on/off toggle button. <br/>
	/// Postcondition: The server will attempt startup/shutdown. (based on if its a toggle on or off respectively)
	/// If the server fails to start/ shutdown, the server on/off toggle button will not change its toggle state.
	/// </remarks>
	[RelayCommand]
	private void ServerStateToggleChanged(bool isToggled)
	{
		if (isToggled)
		{
			ExitCode result = _mainWindowModel.ServerStart();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = false;
			}
		}
		else
		{
			ExitCode result = _mainWindowModel.ServerStop();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = true;
			}
		}
	}
}