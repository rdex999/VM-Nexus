using Client.Services;
using Client.ViewModels.DriveExplorerModes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public partial class DriveExplorerViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	[ObservableProperty]
	private ViewModelBase _explorerModeViewModel;
	
	public DriveExplorerViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		ExplorerModeViewModel = new DrivesViewModel(NavigationSvc, ClientSvc, driveService);
	}
}