using Client.Services;
using Client.ViewModels.DriveExplorerModes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Client.ViewModels;

public partial class DriveExplorerViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	[ObservableProperty]
	private DriveExplorerMode _explorerModeViewModel;
	
	public DriveExplorerViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		ExplorerModeViewModel = new DrivesViewModel(NavigationSvc, ClientSvc, driveService);
		ExplorerModeViewModel.ChangeMode += OnChangeMode;
	}

	private void OnChangeMode(DriveExplorerMode mode)
	{
		ExplorerModeViewModel.ChangeMode -= OnChangeMode;
		ExplorerModeViewModel = mode;
		ExplorerModeViewModel.ChangeMode += OnChangeMode;
	}
}