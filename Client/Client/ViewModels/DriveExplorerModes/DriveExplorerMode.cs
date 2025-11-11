using System;
using Client.Services;

namespace Client.ViewModels.DriveExplorerModes;

public class DriveExplorerMode : ViewModelBase
{
	public Action<DriveExplorerMode>? ChangeMode;
	
	public DriveExplorerMode(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}