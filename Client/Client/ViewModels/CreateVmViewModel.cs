using System;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

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
	
	[ObservableProperty]
	private string _vmCreationMessage = string.Empty;
	
	[ObservableProperty]
	private bool _vmCreationMessageSuccessClass = true;
	
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
	}

	/// <summary>
	/// Called when the operating system input field changes. (Called from the code-behind file)
	/// </summary>
	/// <remarks>
	/// Precondition: The operating system input field value has changed. <br/>
	/// Postcondition: The virtual machines creation settings change accordingly.
	/// </remarks>
	public void OperatingSystemChanged()
	{
		if (OperatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOS)
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

	/// <summary>
	/// Handles a change in the virtual machines creation settings. (disk size, etc)
	/// </summary>
	/// <remarks>
	/// Precondition: A change in one or more of the VM's creation settings input field has happened. <br/>
	/// Postcondition: Error messages are displayed if needed, UI updates accordingly.
	/// </remarks>
	public async Task VmCreationInfoChangedAsync()
	{
		if (OperatingSystem == SharedDefinitions.OperatingSystem.Other)
		{
			OsDriveTypeIsEnabled = false;
			OsDriveSizeIsEnabled = false;
		}
		else
		{
			OsDriveTypeIsEnabled = true;
			OsDriveSizeIsEnabled = true;		
		}
		
		if ((OsDriveSize == null || OsDriveSize > _osDriveSizeMax || OsDriveSize < _osDriveSizeMin) && OperatingSystem != SharedDefinitions.OperatingSystem.Other)
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
		VmCreationMessage = string.Empty;
		
		CreateVmButtonIsEnabled = isVmNameValid && OsDriveSize != null && 
		                          ((OsDriveSize >= _osDriveSizeMin && OsDriveSize <= _osDriveSizeMax) || OperatingSystem == SharedDefinitions.OperatingSystem.Other);
	}

	/// <summary>
	/// Handles a click on the create VM button. Requests the server to create a virtual machine with the inputted settings.
	/// </summary>
	/// <remarks>
	/// Precondition: The user has clicked the create VM button. <br/>
	/// Postcondition: On success, a new virtual machine is created and a success message is displayed. <br/>
	/// On failure, the virtual machine is not created and an according error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task CreateVirtualMachineAsync()
	{
		MessageResponseCreateVm.Status result = await ClientSvc.CreateVirtualMachineAsync(VmName, OperatingSystem, CpuArchitecture, BootMode);
		if (result == MessageResponseCreateVm.Status.Success)
		{
			VmCreationMessageSuccessClass = true;
			VmCreationMessage = "The virtual machine has been created successfully!";
		} 
		else if (result == MessageResponseCreateVm.Status.VmAlreadyExists)
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessage = $"A virtual machine called \"{VmName}\" already exists.";
		}
		else
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessage = "Could not create the virtual machine.";
		}

		CreateVmButtonIsEnabled = false;
	}
}