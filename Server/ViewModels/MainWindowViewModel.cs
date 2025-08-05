using System.Diagnostics;
using System.Threading;
using CommunityToolkit.Mvvm.Input;
using Server.Models;

namespace Server.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
	private readonly MainWindowModel _mainWindowModel;

	public MainWindowViewModel()
	{
		_mainWindowModel = new MainWindowModel();
	}
		
	[RelayCommand]
	private void ServerStateToggleChanged(bool isToggled)
	{
		if(isToggled)
			_mainWindowModel.ServerStart();
		else
			_mainWindowModel.ServerStop();
	}
}