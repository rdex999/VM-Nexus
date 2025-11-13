using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Services;
using Client.ViewModels.DriveExplorerModes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Drives;

namespace Client.ViewModels;

public partial class DriveExplorerViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	public ObservableCollection<PathPartItemTemplate> PathParts { get; }
	private Stack<string> _prevPathParts;
	
	[ObservableProperty]
	private DriveExplorerMode _explorerModeViewModel;

	[ObservableProperty] 
	private bool _prevButtonIsEnabled = false;
	
	[ObservableProperty]
	private bool _nextButtonIsEnabled = false;
	
	public DriveExplorerViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		PathParts = new ObservableCollection<PathPartItemTemplate>();
		_prevPathParts = new Stack<string>();
		ExplorerModeViewModel = new DrivesViewModel(NavigationSvc, ClientSvc, driveService);
		ExplorerModeViewModel.ChangePath += OnChangePathRequested;
	}

	/// <summary>
	/// Changes the current path in the explorer, assigns a new view and lists items if needed.
	/// </summary>
	/// <param name="newPath">The new path to change into. newPath != null.</param>
	/// <remarks>
	/// Precondition: Changing the current path is required. newPath != null. <br/>
	/// Postcondition: On success, the path is changed. On failure, the path is set to "", meaning listing drives.
	/// </remarks>
	private async Task ChangePathAsync(string newPath)
	{
		string newPathTrimmed = newPath.Trim('/');
		string[] pathParts = newPathTrimmed.Split('/');
		bool isGoingBack = pathParts.Length < PathParts.Count || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0]));
		PathParts.Clear();
		PrevButtonIsEnabled = pathParts.Length > 0 && !string.IsNullOrEmpty(pathParts[0]);
		
		if (_prevPathParts.Count > 0 && !isGoingBack)
		{
			if (_prevPathParts.Peek() == pathParts.Last())
				_prevPathParts.Pop();
			else
				_prevPathParts.Clear();
		}

		NextButtonIsEnabled = _prevPathParts.Count > 0;
		if (pathParts.Length == 0 || (pathParts.Length == 1 && string.IsNullOrEmpty(pathParts[0])))
		{
			ChangeExplorerMode(new DrivesViewModel(NavigationSvc, ClientSvc, _driveService));
			return;
		}
		
		string driveName = pathParts[0];
		string path = string.Join('/', pathParts[1..]);
		DriveGeneralDescriptor? driveDescriptor = _driveService.GetDriveByName(driveName);
		if (driveDescriptor == null)
		{
			await ChangePathAsync(string.Empty);	/* Will change into DrivesViewModel */
			return;
		}

		if (driveDescriptor.PartitionTableType == PartitionTableType.Unpartitioned)
		{
			PathItem[]? items = await _driveService.ListItemsOnDrivePathAsync(driveDescriptor.Id, path);
			if (items == null)
			{ 
				await ChangePathAsync(string.Empty); 
				return;
			}

			ChangeExplorerMode(new FileSystemItemsViewModel(NavigationSvc, ClientSvc, _driveService, driveDescriptor, path, items));
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

	/// <summary>
	/// Changes the current explorer mode.
	/// </summary>
	/// <param name="mode">The new mode to change into. mode != null.</param>
	/// <remarks>
	/// Precondition: Changing the current mode is required,
	/// for example the user has entered a drive. (now listing partitions and not drives, change into PartitionsViewModel) mode != null. <br/>
	/// Postcondition: The explorer mode is set to the new mode.
	/// </remarks>
	private void ChangeExplorerMode(DriveExplorerMode mode)
	{
		ExplorerModeViewModel.ChangePath -= OnChangePathRequested;
		ExplorerModeViewModel = mode;
		ExplorerModeViewModel.ChangePath += OnChangePathRequested;
	}

	/// <summary>
	/// Handles a request to change the path, attempts to change the path.
	/// </summary>
	/// <param name="newPath">The new path to change into. newPath != null.</param>
	/// <remarks>
	/// Precondition: A request to change the path was received. (ChangePath event raised) newPath != null. <br/>
	/// Postcondition: An attempt to change the path is performed.
	/// On success, the path is changed. On failure, the path is set to "", meaning listing drives.
	/// </remarks>
	private void OnChangePathRequested(string newPath) => _ = ChangePathAsync(newPath);

	/// <summary>
	/// Handles a click on the prev path button. Goes one path part back.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the prev path button. User is not in root path. ("") <br/>
	/// Postcondition: Path is set to last path without the last path part. (Going one path part back)
	/// </remarks>
	[RelayCommand]
	private async Task PrevPathClick()
	{
		if (PathParts.Count == 0)
		{
			await ChangePathAsync(string.Empty);
			return;
		}
	
		_prevPathParts.Push(PathParts.Last().Name);
		NextButtonIsEnabled = true;
		PathPartItemTemplate[] pathParts = PathParts.ToArray()[..^1];
		string path = string.Empty;
		foreach (PathPartItemTemplate pathPart in pathParts)
			path += pathPart.Name + '/';
		
		await ChangePathAsync(path);
	}

	/// <summary>
	/// Handles a click on the next path button. Goes one path forward. (from the ones the user was just in)
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the next path button. <br/>
	/// Postcondition: An attempt to enter one path forward (into the directory the user was just in) is performed.
	/// On success, the items on the new path are listed. On failure, the path is set to "", meaning drives are listed.
	/// </remarks>
	[RelayCommand]
	private async Task NextPathClick()
	{
		if (_prevPathParts.Count == 0)
		{
			NextButtonIsEnabled = false;
			return;
		}
		
		string path = string.Empty;
		foreach (PathPartItemTemplate pathPart in PathParts)
			path += pathPart.Name + '/';

		path += _prevPathParts.Peek();
		NextButtonIsEnabled = _prevPathParts.Count > 0;
		await ChangePathAsync(path);
	}
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