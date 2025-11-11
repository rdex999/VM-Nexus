using System.Collections.ObjectModel;
using Client.Services;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class PartitionsViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<PathItem> Partitions { get; }

	public PartitionsViewModel(NavigationService navigationService, ClientService clientService, 
		DriveService driveService, PathItem[] partitions) 
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		Partitions = new ObservableCollection<PathItem>(partitions);
	}
}