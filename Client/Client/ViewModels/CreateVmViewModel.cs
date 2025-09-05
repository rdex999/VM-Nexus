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

	private int _osDriveSizeMax = 1024 * 256;
	
	private int _osDriveSizeMin = 1;
	
	[ObservableProperty]
	private bool _osDriveSizeIsEnabled = true;

	[ObservableProperty] 
	private bool _osDriveSizeErrorClass = false;
	
	[ObservableProperty] 
	private string _osDriveSizeErrorMessage = string.Empty;
	
	[ObservableProperty] 
	private SharedDefinitions.DriveType _osDriveType = SharedDefinitions.DriveType.NVMe;
	
	[ObservableProperty]
	private bool _osDriveTypeIsEnabled = true;

	[ObservableProperty]
	private SharedDefinitions.CpuArchitecture _cpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
	
	[ObservableProperty]
	private bool _cpuArchitectureIsEnabled = true;
	
	[ObservableProperty]
	private bool _createVmButtonIsEnabled = true;

	
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}

	public void OperatingSystemChanged()
	{
		if (OperatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOs)
		{
			_osDriveSizeMax = 128;
			_osDriveSizeMin = 1;
			
			OsDriveType =  SharedDefinitions.DriveType.Floppy;
			OsDriveTypeIsEnabled = false;
			
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86;
			CpuArchitectureIsEnabled = false;
		}
		else
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 4;
			OsDriveTypeIsEnabled = true;
			CpuArchitectureIsEnabled = true;
		}
	}
	
	public void VmCreationInfoChanged()
	{
		if (OsDriveSize > _osDriveSizeMax || OsDriveSize < _osDriveSizeMin)
		{
			OsDriveSizeErrorClass = true;
			OsDriveSizeErrorMessage =
				$"For the {Common.SeparateStringWords(OperatingSystem.ToString())} operating " +
				$"system, the disk size should be between {_osDriveSizeMin} and {_osDriveSizeMax} MiB.";
		}
		else
		{
			OsDriveSizeErrorClass = false;
			OsDriveSizeErrorMessage = string.Empty;
		}
		
		CreateVmButtonIsEnabled = !string.IsNullOrEmpty(VmName) && OsDriveSize >= _osDriveSizeMin &&
		                          OsDriveSize <= _osDriveSizeMax;
	}
	
	[RelayCommand]
	private void CreateVirtualMachine()
	{
	}
}