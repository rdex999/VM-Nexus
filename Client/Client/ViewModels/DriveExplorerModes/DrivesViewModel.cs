using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;

namespace Client.ViewModels.DriveExplorerModes;

public partial class DrivesViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	public ObservableCollection<DriveItemTemplate> DriveItems { get; }
	public ObservableCollection<VmConnectionItemTemplate> ConPopupVmConnections { get; }
	private int _conPopupDriveId = -1;

	private readonly bool _driveOpenIsEnabled = true;
	
	[ObservableProperty] 
	private bool _conPopupIsOpen = false;

	[ObservableProperty]
	private bool _newDrivePopupIsOpen = false;

	[ObservableProperty]
	private bool _newDrivePopupCreateIsEnabled = false;  /* Size is 0 by default - invalid */
	
	[ObservableProperty]
	private string _newDrivePopupName = string.Empty;

	[ObservableProperty] 
	private bool _newDrivePopupNameValid = false;

	[ObservableProperty]
	private string _newDrivePopupNameError = string.Empty;
	
	[ObservableProperty] 
	private int? _newDrivePopupSizeMb = 0;

	[ObservableProperty] 
	private bool _newDrivePopupSizeError = true;	/* Size is 0 by default, invalid value */
	
	[ObservableProperty]
	private bool _newDrivePopupImageIsVisible = false;

	[ObservableProperty] 
	private NewDrivePopupDriveType _newDrivePopupType = NewDrivePopupDriveType.Fat32;

	[ObservableProperty]
	private string _newDrivePopupImageSize = string.Empty;
		
	[ObservableProperty]
	private string _newDrivePopupImagePath = string.Empty;

	[ObservableProperty] 
	private string _newDrivePopupCreateError = string.Empty;

	[ObservableProperty] 
	private double _newDrivePopupIsoUploadProgress;

	[ObservableProperty] 
	private bool _newDrivePopupUploadingImage = false;

	private IStorageFile? _newDrivePopupImage;

	public enum NewDrivePopupDriveType
	{
		Fat32,
		Iso,
		DiskImage,
	}
	
	public DrivesViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		DriveItems = new ObservableCollection<DriveItemTemplate>();
		ConPopupVmConnections = new ObservableCollection<VmConnectionItemTemplate>();
		_driveService.Initialized += (_, _) => UpdateDrives();
		ClientSvc.DriveCreated += OnDriveCreated;
		ClientSvc.ItemDeleted += OnItemDeleted;

		if (ClientSvc.IsLoggedInAsSubUser && ClientSvc.User is SubUser subUser)
			_driveOpenIsEnabled = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveItemList);
		
		if (_driveService.IsInitialized)
			UpdateDrives();
	}

	/* Note: Use for IDE preview only. */
	public DrivesViewModel()
	{
		_driveService = null!;
		ConPopupVmConnections = new ObservableCollection<VmConnectionItemTemplate>();
		DriveItems = new ObservableCollection<DriveItemTemplate>()
		{
			new DriveItemTemplate(new DriveGeneralDescriptor(1, "test_vm0 - Ubuntu", 50000, 512, DriveType.Disk, PartitionTableType.GuidPartitionTable), true),
			new DriveItemTemplate(new DriveGeneralDescriptor(2, "test_vm1 - MiniCoffeeOS", 15, 512, DriveType.Floppy, PartitionTableType.Unpartitioned), true),
			new DriveItemTemplate(new DriveGeneralDescriptor(2, "OS iso", 4192, 512, DriveType.CDROM, PartitionTableType.Unpartitioned), true),
		};
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
		if (ClientSvc.IsLoggedInAsSubUser && ClientSvc.User is SubUser subUser)
			if (!subUser.OwnerPermissions.HasPermission(UserPermissions.DriveItemList))
				return;
		
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
			DriveItems.Add(new DriveItemTemplate(descriptor, _driveOpenIsEnabled));
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
		DriveItems.Add(new DriveItemTemplate(descriptor, _driveOpenIsEnabled));
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
	/// Called when validating the drive creation popup input fields is needed.
	/// </summary>
	/// <remarks>
	/// Precondition: Validating the drive creation popup input fields is needed. (For example after a field was changed)
	/// Postcondition: If all fields are valid, the create button is enabled. For each invalid field, an error is displayed.
	/// </remarks>
	private async Task ValidateNewDrivePopupFieldsAsync()
	{
		string name = NewDrivePopupName.Trim();
		bool isNameValid = false;
		if (string.IsNullOrEmpty(name))
			NewDrivePopupNameError = "Drive name must not be empty.";
		else if (_driveService.DriveExists(name))
			NewDrivePopupNameError = $"A drive called \"{name}\" already exists.";
		else
		{
			isNameValid = true;
			NewDrivePopupNameError = string.Empty;
		}

		NewDrivePopupNameValid = isNameValid;

		bool isValidSize;
		if (_newDrivePopupImage == null)
		{
			isValidSize = NewDrivePopupSizeMb != null 
			              && (long)NewDrivePopupSizeMb * 1024L * 1024L >= ((FileSystemType)NewDrivePopupType).DriveSizeMin() 
			              && NewDrivePopupSizeMb <= SharedDefinitions.DriveSizeMbMax;
		}
		else
		{
			await using Stream image = await _newDrivePopupImage.OpenReadAsync();
			long imageSize = image.Length;
			isValidSize = imageSize / 1024 / 1024 <= SharedDefinitions.DriveSizeMbMax;
			
			if (NewDrivePopupType == NewDrivePopupDriveType.Iso)
				isValidSize = isValidSize && imageSize >= FileSystemType.Iso.DriveSizeMin();
			else
				isValidSize = isValidSize && imageSize > 0;
		}
		
		NewDrivePopupSizeError = !isValidSize;
		NewDrivePopupCreateIsEnabled = isValidSize && isNameValid;
	}

	/// <summary>
	/// Called each time the value of the drive name field in the drive creation popup is changed. Validates the input fields.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The user has changed the name fields in the drive creation popup. <br/>
	/// Postcondition: If all fields are valid, the create button is enabled. For each invalid field, an error is displayed.
	/// </remarks>
	partial void OnNewDrivePopupNameChanged(string value) => _ = ValidateNewDrivePopupFieldsAsync();
	
	/// <summary>
	/// Called each time the value of the drive size field in the drive creation popup is changed. Validates the input fields.
	/// </summary>
	/// <param name="value">Unused.</param>
	/// <remarks>
	/// Precondition: The user has changed the value of the drive size in the drive creation popup. <br/>
	/// Postcondition: If all fields are valid, the create button is enabled. For each invalid field, an error is displayed.
	/// </remarks>
	partial void OnNewDrivePopupSizeMbChanged(int? value) => _ = ValidateNewDrivePopupFieldsAsync();

	/// <summary>
	/// Executed each time after NewDrivePopupFileSystem has changed. Makes ISO image file selection is visible if needed.
	/// </summary>
	/// <param name="value">The new value that was assigned.</param>
	/// <remarks>
	/// Precondition: User has selected another filesystem in the drive creation popup. <br/>
	/// Postcondition: If the user has selected the ISO option, the ISO image file selection section is displayed.
	/// </remarks>
	partial void OnNewDrivePopupTypeChanged(NewDrivePopupDriveType value)
	{
		_ = ValidateNewDrivePopupFieldsAsync();
		NewDrivePopupImageIsVisible = value == NewDrivePopupDriveType.Iso || value == NewDrivePopupDriveType.DiskImage;
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
	/// On failure, the VM-drive connection will not change.
	/// </remarks>
	[RelayCommand]
	private async Task ConPopupApplyClickAsync()
	{
		List<Task<ExitCode>> connectionUpdates = new List<Task<ExitCode>>();
		foreach (VmConnectionItemTemplate item in ConPopupVmConnections)
		{
			bool isConnected = _driveService.ConnectionExists(_conPopupDriveId, item.Id);
			if (item.IsChecked && !isConnected)
			{
				connectionUpdates.Add(_driveService.ConnectDriveAsync(_conPopupDriveId, item.Id));
			}
			else if (!item.IsChecked && isConnected)
			{
				connectionUpdates.Add(_driveService.DisconnectDriveAsync(_conPopupDriveId, item.Id));
			}
		}
		
		await Task.WhenAll(connectionUpdates);
		
		CloseConPopup();
	}

	/// <summary>
	/// Handles a click on the Create New Drive button. Opens the drive creation popup.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the Create New Drive button. <br/>
	/// Postcondition: The new drive creation popup is opened.
	/// </remarks>
	[RelayCommand]
	private void CreateNewDriveClick()
	{
		NewDrivePopupIsOpen = true;
		if (NewDrivePopupUploadingImage)
			return;
		
		NewDrivePopupName = string.Empty;
		NewDrivePopupCreateError = string.Empty;
		NewDrivePopupImagePath = string.Empty;
		_newDrivePopupImage?.Dispose();
		_newDrivePopupImage = null;
		NewDrivePopupUploadingImage = false;
		_ = ValidateNewDrivePopupFieldsAsync();
	}

	/// <summary>
	/// Either closes the Create New Drive popup, or called after it is closed.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the popup, or this method was called in order to close the popup. <br/>
	/// Postcondition: The Create New Drive popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseNewDrivePopup()
	{
		NewDrivePopupIsOpen = false;
		if (NewDrivePopupUploadingImage)
			return;
		
		_newDrivePopupImage?.Dispose();
		_newDrivePopupImage = null;
	}

	/// <summary>
	/// Handles a click on the select ISO button on the drive creation popup.
	/// Opens the operating system file picker and asks the user to select a file.
	/// Displays information about the selected file.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked on the select ISO button on the drive creation popup. <br/>
	/// Postcondition: A file picker is opened. After the user selects a file, its size and path are displayed as the selected ISO file.
	/// </remarks>
	[RelayCommand]
	private async Task NewDrivePopupSelectImageClickAsync()
	{
		IStorageProvider storageProvider;
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			storageProvider = desktop.MainWindow!.StorageProvider;
		
		else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
		{
			TopLevel? topLevel = TopLevel.GetTopLevel(singleViewLifetime.MainView);
			if (topLevel == null)
				return;
			
			storageProvider = topLevel.StorageProvider;
		}
		else
			return;
		
		IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });

		if (files.Count != 1)
			return;
			
		_newDrivePopupImage = files[0];
		await using Stream file = await _newDrivePopupImage.OpenReadAsync();
		
		float sizeMib = file.Length / 1024f / 1024f;
		float sizeGib = file.Length / 1024f / 1024f / 1024f;
		
		NewDrivePopupImageSize = sizeMib >= 1024 ? $"{sizeGib:0.##} GiB" : $"{sizeMib:0.##} MiB";
		NewDrivePopupImagePath = _newDrivePopupImage.Path.LocalPath;
		
		await ValidateNewDrivePopupFieldsAsync();
	}

	/// <summary>
	/// Handles a click on the create button on the drive creation popup. Attempts to create the drive.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the create button on the drive creation popup. Drive settings are valid. <br/>
	/// Postcondition: On success, the drive is created and the popup in closed. On failure, an error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task NewDrivePopupCreateClickAsync()
	{
		if (NewDrivePopupSizeMb == null)
			return;

		string name = NewDrivePopupName.Trim();
		NewDrivePopupCreateError = string.Empty;

		if (NewDrivePopupType == NewDrivePopupDriveType.DiskImage || NewDrivePopupType == NewDrivePopupDriveType.Iso)
		{
			if (_newDrivePopupImage == null)
			{
				CloseNewDrivePopup();
				return;
			}

			Stream image = await _newDrivePopupImage.OpenReadAsync();		/* uploadHandler disposes isoImage */
			MessagingService.UploadHandler? uploadHandler = await ClientSvc.CreateDriveFromImageAsync(
				NewDrivePopupName.Trim(), 
				NewDrivePopupType == NewDrivePopupDriveType.Iso ? DriveType.CDROM : DriveType.Disk,
				image
			);
			
			if (uploadHandler == null)
			{
				NewDrivePopupCreateError = "Creating the drive has failed. Try again later.";
				return;
			}

			NewDrivePopupUploadingImage = true;
			NewDrivePopupIsoUploadProgress = 0.0;

			uploadHandler.DataReceived += (sender, _) =>
			{
				if (sender == null || sender is not MessagingService.TransferHandler handler)
					return;

				Dispatcher.UIThread.Post(() => 
					NewDrivePopupIsoUploadProgress = ((double)handler.BytesReceived / handler.Size) * 100.0
				);
			};

			await uploadHandler.Task;
			
			NewDrivePopupUploadingImage = false;
			CloseNewDrivePopup();
		}
		else
		{
			MessageResponseCreateDriveFs.Status result = await ClientSvc.CreateDriveFsAsync(name, NewDrivePopupSizeMb.Value, (FileSystemType)NewDrivePopupType);
			if (result == MessageResponseCreateDriveFs.Status.Success)
			{
				CloseNewDrivePopup();
				return;
			}

			if (result == MessageResponseCreateDriveFs.Status.DriveAlreadyExists)
				NewDrivePopupCreateError = $"A drive called \"{name}\" already exists.";
			else
				NewDrivePopupCreateError = "Creating the drive has failed. Try again later.";
		}
	}
}

public partial class DriveItemTemplate : ObservableObject
{
	public Action<int>? OpenClick;
	public Action<int>? DownloadClick;
	public Action<int>? DeleteClick;
	public Action<int>? ManageVmConnectionsClick;
	public int Id { get; }

	private int _size;		/* In MiB */

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
	private bool _openEnabled;

	[ObservableProperty]
	private string _name;

	[ObservableProperty] 
	private string _sizeString = null!;

	[ObservableProperty] 
	private DriveType _driveType;

	public DriveItemTemplate(DriveGeneralDescriptor descriptor, bool openEnabled)
	{
		Id = descriptor.Id;
		Name = descriptor.Name;
		Size = descriptor.Size;
		DriveType = descriptor.DriveType;
		OpenEnabled = openEnabled;
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