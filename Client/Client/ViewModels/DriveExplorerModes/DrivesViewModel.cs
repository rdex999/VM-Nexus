using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class DrivesViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	public ObservableCollection<DriveItemTemplate> DriveItems { get; }
	
	public DrivesViewModel(NavigationService navigationService, ClientService clientService, DriveService driveService)
		: base(navigationService, clientService)
	{
		_driveService = driveService;
		DriveItems = new ObservableCollection<DriveItemTemplate>();
		driveService.Initialized += (_, _) => UpdateDrives();
		ClientSvc.DriveCreated += (_, descriptor) => DriveItems.Add(new DriveItemTemplate(descriptor));
		ClientSvc.DriveDeleted += OnDriveDeleted;
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
				DriveItems.RemoveAt(i);
		}
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
		}	
	}
}

public partial class DriveItemTemplate : ObservableObject
{
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
}