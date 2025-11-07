using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;
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
		driveService.Initialized += DriveServiceOnInitialized;
	}

	private void DriveServiceOnInitialized(object? sender, ExitCode status)
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
	private int _id;

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

	public DriveItemTemplate(int id, string name, int size, DriveType driveType)
	{
		_id = id;
		Name = name;
		Size = size;
		DriveType = driveType;
	}

	public DriveItemTemplate(DriveGeneralDescriptor descriptor)
	{
		_id = descriptor.Id;
		Name = descriptor.Name;
		Size = descriptor.Size;
		DriveType = descriptor.DriveType;
	}
}