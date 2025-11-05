using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared.Drives;

namespace Client.ViewModels.DriveExplorerModes;

public class DrivesViewModel : ViewModelBase
{
	public ObservableCollection<DriveItemTemplate> DriveItems { get; }
	
	public DrivesViewModel(NavigationService navigationService, ClientService clientService)
		: base(navigationService, clientService)
	{
		DriveItems = new ObservableCollection<DriveItemTemplate>();
		
		/* For testing */
		DriveItems.Add(new DriveItemTemplate(0, "drive1", 26000, DriveType.Disk));
		DriveItems.Add(new DriveItemTemplate(1, "drive2", 1024 * 32, DriveType.Disk));
		DriveItems.Add(new DriveItemTemplate(2, "drive3", 260, DriveType.Floppy));
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
}