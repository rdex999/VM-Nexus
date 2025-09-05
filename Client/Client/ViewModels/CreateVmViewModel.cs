using System;
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
	private int _osDriveSizeMax = 1024 * 256;
	
	[ObservableProperty]
	private int _osDriveSizeMin = 1;
	
	[ObservableProperty]
	private bool _osDriveSizeIsEnabled = true;

	[ObservableProperty] 
	private SharedDefinitions.DriveType _osDriveType = SharedDefinitions.DriveType.NVMe;
	
	[ObservableProperty]
	private bool _osDriveTypeIsEnabled = true;

	[ObservableProperty]
	private SharedDefinitions.CpuArchitecture _cpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
	
	[ObservableProperty]
	private bool _cpuArchitectureIsEnabled = true;
	
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}

	public void OperatingSystemChanged()
	{
		if (OperatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOs)
		{
			OsDriveSizeMax = 128;
			OsDriveSizeMin = 1;
			
			OsDriveType =  SharedDefinitions.DriveType.Floppy;
			OsDriveTypeIsEnabled = false;
			
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86;
			CpuArchitectureIsEnabled = false;
		}
		else
		{
			OsDriveSizeMax = 1024 * 256;
			OsDriveSizeMin = 1024 * 4;
			OsDriveTypeIsEnabled = true;
			CpuArchitectureIsEnabled = true;
		}
	}
	
	[RelayCommand]
	private void CreateVirtualMachine()
	{
	}
}