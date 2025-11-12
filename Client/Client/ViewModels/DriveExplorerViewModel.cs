using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Services;
using Client.ViewModels.DriveExplorerModes;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Drives;

namespace Client.ViewModels;

public partial class DriveExplorerViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	public ObservableCollection<PathPartItemTemplate> PathParts { get; }
	
	[ObservableProperty]
	private DriveExplorerMode _explorerModeViewModel;
	
	public DriveExplorerViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		PathParts = new ObservableCollection<PathPartItemTemplate>();
		ExplorerModeViewModel = new DrivesViewModel(NavigationSvc, ClientSvc, driveService);
		ExplorerModeViewModel.PathChanged += OnPathChanged;
	}

	private async Task ChangePathAsync(string newPath)
	{
		PathParts.Clear();
		string[] pathParts = newPath.Split('/');

		if (pathParts.Length == 0)
		{
			ChangeExplorerMode(new DrivesViewModel(NavigationSvc, ClientSvc, _driveService));
			return;
		}
		
		string driveName = pathParts[0];
		DriveGeneralDescriptor? driveDescriptor = _driveService.GetDriveByName(driveName);
		if (driveDescriptor == null)
		{
			await ChangePathAsync(string.Empty);	/* Will change into DrivesViewModel */
			return;
		}

		if (driveDescriptor.PartitionTableType == PartitionTableType.Unpartitioned)
		{
			PathItem[]? items = await _driveService.ListItemsOnDrivePathAsync(driveDescriptor.Id, string.Empty);
			if (items == null)
			{ 
				await ChangePathAsync(string.Empty); 
				return;
			}

			ChangeExplorerMode(new FileSystemItemsViewModel(NavigationSvc, ClientSvc, _driveService, driveDescriptor, driveName, items));
		}
		else
		{
			DriveExplorerMode mode = null!;
			PathItem[]? items;
			if (pathParts.Length == 1)		/* Only the drive is in the path - show partitions */
			{
				items = await _driveService.ListItemsOnDrivePathAsync(driveDescriptor.Id, string.Empty);

				if (items != null)
					mode = new PartitionsViewModel(NavigationSvc, ClientSvc, _driveService, driveDescriptor, items);
			}
			else
			{
				string path = string.Join('/', pathParts[1..]);
				items = await _driveService.ListItemsOnDrivePathAsync(driveDescriptor.Id, path);
				if (items != null)
					mode = new FileSystemItemsViewModel(NavigationSvc, ClientSvc, _driveService, driveDescriptor, path, items);
			}

			if (items == null)
			{
				await ChangePathAsync(string.Empty);
				return;
			}
			
			ChangeExplorerMode(mode);
		}
		
		foreach (string pathPart in pathParts)
		{
			if (PathParts.LastOrDefault() != null)
				PathParts.Last().IsLast = false;
			
			PathParts.Add(new PathPartItemTemplate(pathPart));
		}
	}

	private void ChangeExplorerMode(DriveExplorerMode mode)
	{
		ExplorerModeViewModel.PathChanged -= OnPathChanged;
		ExplorerModeViewModel = mode;
		ExplorerModeViewModel.PathChanged += OnPathChanged;
	}

	private void OnPathChanged(string newPath) => _ = ChangePathAsync(newPath);
}

public partial class PathPartItemTemplate : ObservableObject
{
	public string Name { get; }

	[ObservableProperty] 
	private bool _isLast;
	
	public PathPartItemTemplate(string name)
	{
		Name = name;
		IsLast = true;
	}
}