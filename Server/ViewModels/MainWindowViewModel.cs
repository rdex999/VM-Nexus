using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Server.Models;
using Shared;

namespace Server.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	public readonly MainWindowModel MainWindowModel;
	
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
		MainWindowModel = new MainWindowModel();
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
	private async Task ServerStateToggleChangedAsync(bool isToggled)
	{
		if (isToggled)
		{
			ExitCode result = await MainWindowModel.ServerStartAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = false;
			}
		}
		else
		{
			ExitCode result = await MainWindowModel.ServerStopAsync();
			if (result != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = true;
			}
		}
	}
}