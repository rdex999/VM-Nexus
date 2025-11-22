using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Client.ViewModels.DriveExplorerModes;

public partial class DrivesViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<DriveItemTemplate> DriveItems { get; }
	public ObservableCollection<VmConnectionItemTemplate> ConPopupVmConnections { get; }
	private int _conPopupDriveId = -1;
	
	[ObservableProperty] 
	private bool _conPopupIsOpen = false;
	
	public DrivesViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		DriveItems = new ObservableCollection<DriveItemTemplate>();
		ConPopupVmConnections = new ObservableCollection<VmConnectionItemTemplate>();
		_driveService.Initialized += (_, _) => UpdateDrives();
		ClientSvc.DriveCreated += OnDriveCreated;
		ClientSvc.ItemDeleted += OnItemDeleted;
		
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
			DriveItems.Last().OpenClick += OnDriveOpenClicked;
			DriveItems.Last().DownloadClick += OnDriveDownloadRequested;
			DriveItems.Last().DeleteClick += OnDriveDeleteRequested;
			DriveItems.Last().ManageVmConnectionsClick += OnManageVmConnectionsClick;
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
		DriveItems.Last().OpenClick += OnDriveOpenClicked;
		DriveItems.Last().DownloadClick += OnDriveDownloadRequested;
		DriveItems.Last().DeleteClick += OnDriveDeleteRequested;
		DriveItems.Last().ManageVmConnectionsClick += OnManageVmConnectionsClick;
	}

	/// <summary>
	/// Handles a drive deletion event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="info">The item deletion info. info != null.</param>
	/// <remarks>
	/// Precondition: One of the user's drives was deleted. info != null. <br/>
	/// Postcondition: Event is handled, item is removed from display.
	/// </remarks>
	private void OnItemDeleted(object? sender, MessageInfoItemDeleted info)
	{
		if (!Common.IsPathToDrive(info.Path))
			return;
		
		for (int i = 0; i < DriveItems.Count; ++i)
		{
			if (DriveItems[i].Id == info.DriveId)
			{
				DriveItems[i].OpenClick -= OnDriveOpenClicked;
				DriveItems[i].DownloadClick -= OnDriveDownloadRequested;
				DriveItems[i].DeleteClick -= OnDriveDeleteRequested;
				DriveItems[i].ManageVmConnectionsClick -= OnManageVmConnectionsClick;
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

	/// <summary>
	/// Handles a drive delete request. Open the drive deletion dialog.
	/// </summary>
	/// <param name="driveId">The ID of the drive to delete. driveId >= 1.</param>
	/// <remarks>
	/// Precondition: User has clicked on the delete button on a drive. driveId >= 1. <br/>
	/// Postcondition: Drive deletion dialog is displayed.
	/// </remarks>
	private void OnDriveDeleteRequested(int driveId) => DeleteItem?.Invoke(driveId, string.Empty);

	/// <summary>
	/// Handles a click on the VM connections management button on a drive. Opens the VM connection management popup.
	/// </summary>
	/// <param name="driveId">The ID of the drive that the VM connections management button was clicked on. driveId >= 1.</param>
	/// <remarks>
	/// Precondition: User has clicked on the VM connections management button on a drive. driveId >= 1. <br/>
	/// Postcondition: The VM connections management popup is open.
	/// </remarks>
	private void OnManageVmConnectionsClick(int driveId)
	{
		_conPopupDriveId = driveId;
		VmGeneralDescriptor[] descriptors = _driveService.GetVirtualMachines();
		ConPopupVmConnections.Clear();
		foreach (var descriptor in descriptors)
		{
			bool connected = _driveService.ConnectionExists(driveId, descriptor.Id);
			ConPopupVmConnections.Add(new VmConnectionItemTemplate(descriptor, connected));
		}
		ConPopupIsOpen = true;
	}

	/// <summary>
	/// Either closes the VM connection popup, or called after it is closed.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the popup, or this method was called in order to close the popup. <br/>
	/// Postcondition: VM connection management popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseConPopup() => ConPopupIsOpen = false;

	/// <summary>
	/// Handles a click on the apply button on the VM connection management popup. Attempts to apply the new changes.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the apply button on the VM connection management popup. <br/>
	/// Postcondition: Each virtual machine that its connection state to this drive was changed, will be updated to the new state.
	/// On failure, an error will be shown for each failed virtual machine.
	/// </remarks>
	[RelayCommand]
	private async Task ConPopupApplyClickAsync()
	{
		foreach (VmConnectionItemTemplate item in ConPopupVmConnections)
		{
			bool isConnected = _driveService.ConnectionExists(_conPopupDriveId, item.Id);
			if (item.IsChecked && !isConnected)
			{
				/* TODO: Connect drive. */
			}
			else if (!item.IsChecked && isConnected)
			{
				/* TODO: Disconnect drive. */
			}
		}
		
		CloseConPopup();
	}
}

public partial class DriveItemTemplate : ObservableObject
{
	public Action<int>? OpenClick;
	public Action<int>? DownloadClick;
	public Action<int>? DeleteClick;
	public Action<int>? ManageVmConnectionsClick;
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
	public void Open() => OpenClick?.Invoke(Id);

	/// <summary>
	/// Handles a click on the download button on a drive. Opens a save-file dialog and downloads the disk image.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the download button on this drive. <br/>
	/// Postcondition: A save-file dialog is opened and a download is started.
	/// </remarks>
	[RelayCommand]
	private void Download() => DownloadClick?.Invoke(Id);

	/// <summary>
	/// Handles a click on the delete button of this drive. Displays delete confirmation dialog.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the delete button on this drive. <br/>
	/// Postcondition: Drive deletion dialog is displayed.
	/// </remarks>
	[RelayCommand]
	private void Delete() => DeleteClick?.Invoke(Id);

	/// <summary>
	/// Handles a click on the manage VM connections button of this drive. Displays the connection management popup.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the manage VM connections button of this drive. <br/>
	/// Postcondition: VM connection management popup is displayed.
	/// </remarks>
	[RelayCommand]
	private void ManageVmConnections() => ManageVmConnectionsClick?.Invoke(Id);
}

public partial class VmConnectionItemTemplate : ObservableObject
{
	public int Id { get; }
	public string Name { get; }
	public string OperatingSystem { get; }

	[ObservableProperty] 
	private bool _isChecked;

	public VmConnectionItemTemplate(VmGeneralDescriptor descriptor, bool connected)
	{
		Id = descriptor.Id;
		Name = descriptor.Name;
		OperatingSystem = Common.SeparateStringWords(descriptor.OperatingSystem.ToString());
		IsChecked = connected;
	}
}