using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace Client.ViewModels;

public partial class CreateVmViewModel : ViewModelBase
{
	[ObservableProperty] 
	private string _vmName = string.Empty;

	[ObservableProperty] 
	private SharedDefinitions.OperatingSystem _operatingSystem = SharedDefinitions.OperatingSystem.Ubuntu;

	[ObservableProperty] 
	private int _osDriveSize = 20480;

	[ObservableProperty] 
	private SharedDefinitions.DriveType _osDriveType = SharedDefinitions.DriveType.NVMe;

	[ObservableProperty]
	private SharedDefinitions.CpuArchitecture _cpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
	
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}
	
	[RelayCommand]
	private void CreateVirtualMachine()
	{
	}
}