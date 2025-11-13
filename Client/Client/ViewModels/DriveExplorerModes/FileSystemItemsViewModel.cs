using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Client.Services;
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

		PathChanged?.Invoke($"{_driveDescriptor.Name}/{_path}/{item.Name}");
	}
}

public class FileSystemItemItemTemplate		/* FilesystemItem - item template (i know) */
{
	public bool IsFile { get; private set; }				/* Is this a file or a directory? */
	public bool IsDirectory
	{
		get => !IsFile;
		private set => IsFile = !value;
	}
	public string Name { get; }
	public long SizeBytes { get; }			/* Only for files, -1 for directories. */
	public DateTime? Accessed { get; }		/* Only for files, null for directories. */
	public DateTime Modified { get; }
	public DateTime Created { get; }
	public Geometry Icon { get; set; } = null!;		/* Set in code-behind. */

	/* Use for files. */
	public FileSystemItemItemTemplate(string name, long sizeBytes, DateTime accessed, DateTime modified, DateTime created)
	{
		IsFile = true;
		Name = name;
		SizeBytes = sizeBytes;
		Accessed = accessed;
		Modified = modified;
		Created = created;
	}

	/* Use for directories. */
	public FileSystemItemItemTemplate(string name, DateTime modified, DateTime created)
	{
		IsFile = false;
		Name = name;
		SizeBytes = -1;
		Accessed = null;
		Modified = modified;
		Created = created;
	}
}