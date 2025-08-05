using System.Diagnostics;
using System.Threading;
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
		
	[RelayCommand]
	private void ServerStateToggleChanged(bool isToggled)
	{
		if (isToggled)
		{
			ExitCode code = _mainWindowModel.ServerStart();
			if (code != ExitCode.Success)
			{
				/* TODO: Add logic to display error message */
				ServerStateIsChecked = false;
			}
		}
		else
		{
			_mainWindowModel.ServerStop();
		}
	}
}