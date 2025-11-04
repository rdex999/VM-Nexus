using System.Collections.ObjectModel;
using Client.Services;
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
		DriveItems.Add(new DriveItemTemplate(new DriveGeneralDescriptor(0, "drive1", 32000, DriveType.Disk)));
		DriveItems.Add(new DriveItemTemplate(new DriveGeneralDescriptor(1, "drive2", 32000, DriveType.Floppy)));
	}
}

public class DriveItemTemplate
{
	public DriveGeneralDescriptor Descriptor { get; }
	
	public DriveItemTemplate(DriveGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}