using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;

namespace Client.ViewModels.DriveExplorerModes;

public partial class FileSystemItemsViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	private readonly DriveGeneralDescriptor _driveDescriptor;
	private readonly string _path;
	
	public ObservableCollection<FileSystemItemItemTemplate> Items { get; }
	public bool CanCreateItems { get; } = true;
	
	[ObservableProperty] 
	private bool _directoryCreatePopupIsOpen = false;
	
	[ObservableProperty]
	private string _directoryCreateName = string.Empty;

	[ObservableProperty] 
	private bool _directoryCreateError = false;
	
	public FileSystemItemsViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService, 
		DriveGeneralDescriptor driveDescriptor, string path, PathItem[] items)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		_driveDescriptor = driveDescriptor;
		_path = string.IsNullOrEmpty(path)
			? path
			: path.Trim(SharedDefinitions.DirectorySeparators);
		
		Items = new ObservableCollection<FileSystemItemItemTemplate>();

		if (ClientSvc.IsLoggedInAsSubUser && ClientSvc.User is SubUser subUser)
		{
			CanCreateItems = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveItemCreate);
			FileSystemItemItemTemplate.CanDownload = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveItemDownload);
			FileSystemItemItemTemplate.CanDelete = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveItemDelete);
		}

		foreach (PathItem item in items)
		{
			if (item is PathItemFile file)
				Items.Add(new FileSystemItemItemTemplate(file.Name, file.SizeBytes, file.Accessed, file.Modified, file.Created));
			
			else if (item is PathItemDirectory directory)
				Items.Add(new FileSystemItemItemTemplate(directory.Name, directory.Modified, directory.Created));
			
			Items.Last().DownloadRequested += filename => DownloadItem?.Invoke(_driveDescriptor.Id, _path + '/' + filename);
			Items.Last().DeleteRequested += filename => DeleteItem?.Invoke(_driveDescriptor.Id, _path + '/' + filename);
		}
	}

	/* Use for IDE preview only. */
	public FileSystemItemsViewModel()
	{
		_driveService = null!;
		_driveDescriptor = null!;
		_path = string.Empty;
		CanCreateItems = true;
		FileSystemItemItemTemplate.CanDownload = true;
		FileSystemItemItemTemplate.CanDelete = true;
		
		Items = new ObservableCollection<FileSystemItemItemTemplate>()
		{
			new FileSystemItemItemTemplate("folder", DateTime.Now, DateTime.Now),
			new FileSystemItemItemTemplate("file", 10000, DateTime.Now, DateTime.Now, DateTime.Now),
			new FileSystemItemItemTemplate("document.txt", 512, DateTime.Now, DateTime.Now, DateTime.Now),
		};
	}

	/// <summary>
	/// Handles a double tap on a filesystem item.
	/// </summary>
	/// <param name="item"></param>
	/// <remarks>
	/// Precondition: A filesystem item was double-tapped. item != null. <br/>
	/// Postcondition: If the item is a directory, an attempt to enter it is performed.
	/// </remarks>
	public void FileSystemItemDoubleTapped(FileSystemItemItemTemplate item)
	{
		if (!item.IsDirectory)
			return;

		if (string.IsNullOrEmpty(_path))
			ChangePath?.Invoke($"{_driveDescriptor.Name}/{item.Name}");
		else
			ChangePath?.Invoke($"{_driveDescriptor.Name}/{_path}/{item.Name}");
	}

	/// <summary>
	/// Displays a file picker dialog, and attempts to upload the selected file into the drive at the current path.
	/// </summary>
	/// <remarks>
	/// Precondition: User is inside a drive, and has clicked on the upload button. <br/>
	/// Postcondition: A file picker dialog appears. After the user has selected a file,
	/// attempting to upload the file into the drive at the current path. On failure, the file is not uploaded.
	/// </remarks>
	[RelayCommand]
	private async Task UploadFileClick()
	{
		Stream stream;
		IStorageProvider? provider = null;
		IStorageFile? file = null;
		
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			provider = desktop.MainWindow!.StorageProvider;
		
		else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
		{
			TopLevel? topLevel = TopLevel.GetTopLevel(singleViewLifetime.MainView);
			if (topLevel != null)
				provider = topLevel.StorageProvider;
		}

		if (provider == null)
			return;

		file = (await provider.OpenFilePickerAsync(new FilePickerOpenOptions()
		{
			AllowMultiple = false
		})).FirstOrDefault();

		if (file == null)
			return;

		stream = await file.OpenReadAsync();
	
		string path = _path + '/' + file.Name;
		(MessageResponseUploadFile.Status result, MessagingService.UploadHandler? handler) = await ClientSvc.StartFileUploadAsync(_driveDescriptor.Id, path, stream);

		if (result == MessageResponseUploadFile.Status.Success)
			_ = handler!.Task.ContinueWith(_ => file?.Dispose());
		else
		{
			await stream.DisposeAsync();
			file.Dispose();
		}
		
		/* TODO: Add an upload/download progress bar at the bottom right corner, with error messages too. */
	}

	/// <summary>
	/// Handles a click on the create directory context menu button.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the create directory context menu button. <br/>
	/// Postcondition: A directory creation popup is shows, prompting for the directory's name.
	/// </remarks>
	[RelayCommand]
	private void CreateDirectoryClick()
	{
		DirectoryCreateName = "New Directory";
		DirectoryCreatePopupIsOpen = true;
		DirectoryCreateError = false;
	}

	/// <summary>
	/// Handles a click on the enter key while focused on the new directory name text box. Attempts to create the directory.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the enter key while being focused on the new directory name text box. <br/>
	/// Postcondition: An attempt to create the directory was performed. On success, the popup is closed and the directory is created.
	/// On failure, the directory is not created and a red error indication is shown on the directory name text box.
	/// </remarks>
	[RelayCommand]
	private async Task CreateDirectoryConfirmClickAsync()
	{
		MessageResponseCreateDirectory.Status result = await ClientSvc.CreateDirectoryAsync(_driveDescriptor.Id, _path + '/' + DirectoryCreateName);
		DirectoryCreatePopupIsOpen = result != MessageResponseCreateDirectory.Status.Success;
		DirectoryCreateError = DirectoryCreatePopupIsOpen;
	}

	/// <summary>
	/// Handles a click on the escape key while being focused on the new directory name text box.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the escape key while being focused on the new directory name text box. <br/>
	/// Postcondition: The new directory name popup is closed.
	/// </remarks>
	[RelayCommand]
	public void CreateDirectoryExitClick() => DirectoryCreatePopupIsOpen = false;
}

public partial class FileSystemItemItemTemplate		/* FilesystemItem - item template (I know) */
{
	public Action<string>? DownloadRequested;
	public Action<string>? DeleteRequested;
	public static bool CanDownload { get; set; } = true;
	public static bool CanDelete { get; set; } = true;
	public bool IsFile { get; private set; }		/* Is this a file or a directory? */
	public bool IsDirectory
	{
		get => !IsFile;
		private set => IsFile = !value;
	}
	public string Name { get; }
	public ulong SizeBytes { get; }					/* Only for files, -1 for directories. */
	public DateTime? Accessed { get; }				/* Only for files, null for directories. */
	public DateTime Modified { get; }
	public DateTime Created { get; }
	public Geometry Icon { get; set; } = null!;		/* Set in code-behind. */
	public ContextMenu? Menu { get; private set; } = null;

	/* Use for files. */
	public FileSystemItemItemTemplate(string name, ulong sizeBytes, DateTime accessed, DateTime modified, DateTime created)
	{
		IsFile = true;
		Name = name;
		SizeBytes = sizeBytes;
		Accessed = accessed;
		Modified = modified;
		Created = created;
		InitializeContextMenu();
	}

	/* Use for directories. */
	public FileSystemItemItemTemplate(string name, DateTime modified, DateTime created)
	{
		IsDirectory = true;
		Name = name;
		SizeBytes = ulong.MaxValue;
		Accessed = null;
		Modified = modified;
		Created = created;
		InitializeContextMenu();
	}

	/// <summary>
	/// Initialize the context menu for this file/directory. <br/>
	/// Note: Directories cannot be downloaded and thus do not have a context menu.
	/// </summary>
	/// <remarks>
	/// Precondition: Called from constructor of this class. <br/>
	/// Postcondition: If this item is a file, it will have a context menu.
	/// If this item is a directory, it will not have a context menu.
	/// </remarks>
	private void InitializeContextMenu()
	{
		if (IsFile)
		{
			Menu = new ContextMenu()
			{
				Background = new SolidColorBrush(Colors.White),
				Items =
				{
					new MenuItem
					{
						Header = "Download",
						IsEnabled = CanDownload,
						Command = DownloadCommand
					},
					new MenuItem
					{
						Header = "Delete",
						IsEnabled = CanDelete,
						Command = DeleteCommand
					}
				}
			};
		}
		else
		{
			Menu = new ContextMenu()
			{
				Background = new SolidColorBrush(Colors.White),
				Items =
				{
					new MenuItem
					{
						Header = "Delete",
						IsEnabled = CanDelete,
						Command = DeleteCommand
					}
				}
			};
		}
	}

	/// <summary>
	/// Handles a click on the download button on this item. Starts download procedure.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the download button on this item. <br/>
	/// Postcondition: Download procedure is started - save-file dialog opens.
	/// </remarks>
	[RelayCommand]
	private void Download() => DownloadRequested?.Invoke(Name);
	
	/// <summary>
	/// Handles a click on the delete button on this item. The delete confirmation popup is displayed.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the delete button on this item. <br/>
	/// Postcondition: The delete confirmation popup is displayed.
	/// </remarks>
	[RelayCommand]
	private void Delete() => DeleteRequested?.Invoke(IsFile ? Name : (Name + '/'));
}