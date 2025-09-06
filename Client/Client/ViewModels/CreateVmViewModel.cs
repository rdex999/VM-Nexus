using System;
using System.Threading.Tasks;
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
	private string _vmNameErrorMessage = "The name of the virtual machine must not be empty.";

	[ObservableProperty] 
	private bool _vmNameErrorClass = true;

	[ObservableProperty] 
	private SharedDefinitions.OperatingSystem _operatingSystem = SharedDefinitions.OperatingSystem.Ubuntu;
	
	[ObservableProperty]
	private SharedDefinitions.CpuArchitecture _cpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
	
	[ObservableProperty]
	private bool _cpuArchitectureIsEnabled = true;

	[ObservableProperty]
	private SharedDefinitions.BootMode _bootMode = SharedDefinitions.BootMode.Uefi;
	
	[ObservableProperty]
	private bool _bootModeIsEnabled = true;
	
	[ObservableProperty] 
	private SharedDefinitions.DriveType _osDriveType = SharedDefinitions.DriveType.NVMe;
    	
	[ObservableProperty]
	private bool _osDriveTypeIsEnabled = true;
	
	[ObservableProperty] 
	private int? _osDriveSize = 20480;

	private int _osDriveSizeMax = 1024 * 256;
	
	private int _osDriveSizeMin = 1;
	
	[ObservableProperty]
	private bool _osDriveSizeIsEnabled = true;

	[ObservableProperty] 
	private bool _osDriveSizeErrorClass = false;
	
	[ObservableProperty] 
	private string _osDriveSizeErrorMessage = string.Empty;
	
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
			
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86;
			CpuArchitectureIsEnabled = false;
		
			BootMode = SharedDefinitions.BootMode.Bios;
			BootModeIsEnabled = false;
			
			OsDriveType =  SharedDefinitions.DriveType.Floppy;
			OsDriveTypeIsEnabled = false;
		}
		else
		{
			CpuArchitectureIsEnabled = true;
			BootModeIsEnabled = true;
			OsDriveTypeIsEnabled = true;
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 4;
		}
	}

	public async Task VmCreationInfoChangedAsync()
	{
		if (OsDriveSize == null || OsDriveSize > _osDriveSizeMax || OsDriveSize < _osDriveSizeMin)
		{
			OsDriveSizeErrorClass = true;
			OsDriveSizeErrorMessage =
				$"For the {Common.SeparateStringWords(OperatingSystem.ToString())} operating " +
				$"system, the disk size must be between {_osDriveSizeMin} and {_osDriveSizeMax} MiB.";
		}
		else
		{
			OsDriveSizeErrorClass = false;
			OsDriveSizeErrorMessage = string.Empty;
		}

		bool isVmNameValid = false;
		if (string.IsNullOrEmpty(VmName))
		{
			VmNameErrorClass = true;
			VmNameErrorMessage = "The name of the virtual machine must not be empty.";
		} 
		else if (await ClientSvc.IsVmExistsAsync(VmName))
		{
			VmNameErrorClass = true;
			VmNameErrorMessage = "A virtual machine with that name already exists.";
		}
		else
		{
			VmNameErrorClass = false;
			VmNameErrorMessage = string.Empty;
			isVmNameValid = true;
		}
		
		CreateVmButtonIsEnabled = isVmNameValid && OsDriveSize != null && OsDriveSize >= _osDriveSizeMin && OsDriveSize <= _osDriveSizeMax;
	}
	
	[RelayCommand]
	private async Task CreateVirtualMachineAsync()
	{
	}
}