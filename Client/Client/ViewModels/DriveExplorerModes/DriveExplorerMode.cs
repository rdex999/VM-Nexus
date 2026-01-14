using System;
using Client.Services;

namespace Client.ViewModels.DriveExplorerModes;

public class DriveExplorerMode : ViewModelBase
{
	public Action<string>? ChangePath;
	public Action<int, string>? DownloadItem;		/* Drive ID, Path */
	public Action<int, string>? DeleteItem;
	
	public DriveExplorerMode(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
	}

	/* Note: Use for IDE preview only. */
	public DriveExplorerMode()
	{
	}
}