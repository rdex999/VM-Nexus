using System;
using Client.Services;

namespace Client.ViewModels.DriveExplorerModes;

public class DriveExplorerMode : ViewModelBase
{
	public Action<string>? ChangePath;
	public Action<int, string>? DownloadItem;		/* Drive ID, Path */
	
	public DriveExplorerMode(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}
}