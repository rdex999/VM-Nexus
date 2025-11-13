using System;
using Client.Services;

namespace Client.ViewModels.DriveExplorerModes;

public class DriveExplorerMode : ViewModelBase
{
	public Action<string>? ChangePath;
	
	public DriveExplorerMode(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}