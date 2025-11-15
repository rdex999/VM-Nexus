using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class DrivesViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<DriveItemTemplate> DriveItems { get; }
	
	public DrivesViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		DriveItems = new ObservableCollection<DriveItemTemplate>();
		_driveService.Initialized += (_, _) => UpdateDrives();
		ClientSvc.DriveCreated += OnDriveCreated;
		ClientSvc.DriveDeleted += OnDriveDeleted;
		
		if (_driveService.IsInitialized)
			UpdateDrives();
	}

	/// <summary>
	/// Attempts to get the top level items of the drive, and redirect to a corresponding view.
	/// </summary>
	/// <param name="driveId">The ID of the drive to open. driveId >= 1.</param>
	/// <remarks>
	/// Precondition: A drive should be opened. driveId >= 1. <br/>
	/// Postcondition: On success, the user is redirected to a page listing the top level items on the drive.
	/// (partitions if partitioned, root directory items if not partitioned.) <br/>
	/// On failure, the user is not redirected to the page.
	/// </remarks>
	private async Task OpenDriveAsync(int driveId)
	{
		PathItem[]? items = await _driveService.ListItemsOnDrivePathAsync(driveId, string.Empty);
		if (items == null)
			return;

		DriveGeneralDescriptor? descriptor = _driveService.GetDriveById(driveId);
		if (descriptor == null)
			return;

		ChangePath?.Invoke($"{descriptor.Name}");
	}
	
	/// <summary>
	/// Updates the list of the user's drives.
	/// </summary>
	/// <remarks>
	/// Precondition: DriveService is initialized. <br/>
	/// Postcondition: The user's drives are fetched and displayed.
	/// </remarks>
	private void UpdateDrives()
	{
		DriveItems.Clear();
		foreach (DriveGeneralDescriptor descriptor in _driveService.GetDrives())
		{
			DriveItems.Add(new DriveItemTemplate(descriptor));
			DriveItems.Last().Opened += OnDriveOpenClicked;
			DriveItems.Last().DownloadRequested += OnDriveDownloadRequested;
		}	
	}

	/// <summary>
	/// Handles a new drive created event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="descriptor">A descriptor of the new drive. descriptor != null.</param>
	/// <remarks>
	/// Precondition: A new drive was created. descriptor != null. <br/>
	/// Postcondition: Event is handled, the drive is added to the displayed drives.
	/// </remarks>
	private void OnDriveCreated(object? sender, DriveGeneralDescriptor descriptor)
	{
		DriveItems.Add(new DriveItemTemplate(descriptor));
		DriveItems.Last().Opened += OnDriveOpenClicked;
		DriveItems.Last().DownloadRequested += OnDriveDownloadRequested;
	}
	
	/// <summary>
	/// Handles a drive deletion event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the drive that was deleted. id >= 1.</param>
	/// <remarks>
	/// Precondition: One of the user's drives was deleted. id >= 1. <br/>
	/// Postcondition: Event is handled, drive is removed from display.
	/// </remarks>
	private void OnDriveDeleted(object? sender, int id)
	{
		for (int i = 0; i < DriveItems.Count; ++i)
		{
			if (DriveItems[i].Id == id)
			{
				DriveItems[i].Opened -= OnDriveOpenClicked;
				DriveItems[i].DownloadRequested -= OnDriveDownloadRequested;
				DriveItems.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Handles a click on the open button on a drive, and a double click on a drive.
	/// Attempts to get the top level items of the drive, and redirect to a corresponding view.
	/// </summary>
	/// <param name="driveId">The ID of the drive that should be opened. driveId >= 1.</param>
	/// <remarks>
	/// Precondition: The user has either double-clicked on a drive, or clicked on its open button. driveId >= 1. <br/>
	/// Postcondition: Attempts to get the top level items of the drive, and redirect to a corresponding view.
	/// </remarks>
	private void OnDriveOpenClicked(int driveId) => _ = OpenDriveAsync(driveId);

	/// <summary>
	/// Handles a drive download request. Starts the download procedure.
	/// </summary>
	/// <param name="driveId">The ID of the drive to download. driveId >= 1.</param>
	/// <remarks>
	/// Precondition: User has clicked on the download button on a drive. driveId >= 1. <br/>
	/// Postcondition: Download procedure is started, the user is asked where to save the file to.
	/// </remarks>
	private void OnDriveDownloadRequested(int driveId) => DownloadItem?.Invoke(driveId, string.Empty);
}

public partial class DriveItemTemplate : ObservableObject
{
	public Action<int>? Opened;
	public Action<int>? DownloadRequested;
	public int Id { get; }

	private int _size;

	public int Size
	{
		get => _size;
		set
		{
			_size = value;
			SizeString = _size >= 1024 
				? $"{(Size/1024.0):0.##} GiB" 
				: $"{Size} MiB";
		}
	}

	[ObservableProperty]
	private string _name;

	[ObservableProperty] 
	private string _sizeString = null!;

	[ObservableProperty] 
	private DriveType _driveType;

	public DriveItemTemplate(DriveGeneralDescriptor descriptor)
	{
		Id = descriptor.Id;
		Name = descriptor.Name;
		Size = descriptor.Size;
		DriveType = descriptor.DriveType;
	}

	/// <summary>
	/// Invokes the Opened event, in order to open the drive.
	/// </summary>
	/// <remarks>
	/// Precondition: The drive should be opened. The user has either clicked on the open button or double-clicked on the drive. <br/>
	/// Postcondition: The Opened event is invoked, an attempt to open the drive is performed.
	/// </remarks>
	[RelayCommand]
	public void Open() => Opened?.Invoke(Id);

	/// <summary>
	/// Handles a click on the download button on a drive. Opens a save-file dialog and downloads the disk image.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the download button on this drive. <br/>
	/// Postcondition: A save-file dialog is opened and a download is started.
	/// </remarks>
	[RelayCommand]
	private void Download() => DownloadRequested?.Invoke(Id);
}