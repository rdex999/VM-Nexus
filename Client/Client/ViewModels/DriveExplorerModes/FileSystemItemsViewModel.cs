using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Client.Services;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class FileSystemItemsViewModel : DriveExplorerMode
{
	private readonly DriveService _driveService;
	private readonly DriveGeneralDescriptor _driveDescriptor;
	private readonly string _path;
	
	public ObservableCollection<FileSystemItemItemTemplate> Items { get; }
	
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
}

public partial class FileSystemItemItemTemplate		/* FilesystemItem - item template (i know) */
{
	public Action<string>? DownloadRequested;
	public Action<string>? DeleteRequested;
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
		if (!IsFile)
			return;

		Menu = new ContextMenu()
		{
			Background = new SolidColorBrush(Colors.White),
			Items =
			{
				new MenuItem
				{
					Header = "Download",
					Command = DownloadCommand
				},
				new MenuItem
				{
					Header = "Delete",
					Command = DeleteCommand
				}
			}
		};
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
	private void Delete() => DeleteRequested?.Invoke(Name);
}