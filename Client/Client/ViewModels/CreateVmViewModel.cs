using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Client.ViewModels;

public partial class CreateVmViewModel : ViewModelBase
{
	private readonly DriveService _driveService;
	
	[ObservableProperty] 
	private string _vmName = string.Empty;
	
	[ObservableProperty]
	private string _vmNameErrorMessage = "The name of the virtual machine must not be empty.";

	[ObservableProperty] 
	private bool _vmNameErrorClass = true;

	[ObservableProperty] 
	private OperatingSystem _operatingSystem = OperatingSystem.Ubuntu;
	
	[ObservableProperty]
	private bool _osSetupMessageIsVisible = true;
	
	[ObservableProperty]
	private CpuArchitecture _cpuArchitecture = CpuArchitecture.X86_64;
	
	[ObservableProperty]
	private bool _cpuArchitectureIsEnabled = true;

	[ObservableProperty] 
	private int? _ramSizeMiB = 0;

	[ObservableProperty] 
	private bool _ramSizeValid = false;

	[ObservableProperty]
	private BootMode _bootMode = BootMode.Uefi;
	
	[ObservableProperty]
	private bool _bootModeIsEnabled = true;
	
	[ObservableProperty] 
	private DriveType _osDriveType = DriveType.Disk;
    	
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
	private bool _osDriveSettingsIsVisible = true;
	
	[ObservableProperty]
	private bool _createVmButtonIsEnabled = true;
	
	[ObservableProperty]
	private string _vmCreationMessage = string.Empty;
	
	[ObservableProperty]
	private bool _vmCreationMessageSuccessClass = true;
	
	[ObservableProperty]
	private bool _vmCreationMessageErrorClass = false;
	
	public CreateVmViewModel(NavigationService navigationSvc, ClientService clientSvc, DriveService driveService)
		: base(navigationSvc, clientSvc)
	{
		ClientSvc.UserDataChanged += OnUserDataChanged;
		_driveService = driveService;
		CheckCreationPermissions();
	}

	/* Use for IDE preview only. */
	public CreateVmViewModel()
	{
		_driveService = null!;
	}

	private void OnUserDataChanged(object? sender, User user)
	{
		if (ClientSvc.User != null && user.Id == ClientSvc.User.Id)
			CheckCreationPermissions();
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
		if (OperatingSystem == OperatingSystem.MiniCoffeeOS)
		{
			_osDriveSizeMax = 10;
			_osDriveSizeMin = 9;
			
			CpuArchitecture = CpuArchitecture.X86;
			CpuArchitectureIsEnabled = false;
		
			BootMode = BootMode.Bios;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Floppy;
			OsDriveTypeIsEnabled = false;
			OsDriveSizeIsEnabled = true;

			OsSetupMessageIsVisible = false;

			OsDriveSettingsIsVisible = true;
		}
		else if (OperatingSystem == OperatingSystem.Other)
		{
			CpuArchitectureIsEnabled = true;
			BootModeIsEnabled = true;
			
			OsDriveTypeIsEnabled = false;
			OsDriveSizeIsEnabled = false;
			OsSetupMessageIsVisible = false;
			OsDriveSettingsIsVisible = false;
		}
		else if (OperatingSystem == OperatingSystem.Ubuntu)
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 25;
		
			CpuArchitecture = CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = BootMode.Uefi;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Disk;
			OsDriveTypeIsEnabled = false;
			
			OsDriveSizeIsEnabled = true;
			OsSetupMessageIsVisible = true;

			OsDriveSettingsIsVisible = true;
		}
		else if (OperatingSystem == OperatingSystem.KaliLinux)
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 20;
		
			CpuArchitecture = CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = BootMode.Uefi;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Disk;
			OsDriveTypeIsEnabled = false;
			
			OsDriveSizeIsEnabled = true;
			OsSetupMessageIsVisible = true;

			OsDriveSettingsIsVisible = true;	
		}
		else if (OperatingSystem == OperatingSystem.ManjaroLinux)
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 30;
		
			CpuArchitecture = CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = BootMode.Uefi;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Disk;
			OsDriveTypeIsEnabled = false;
			
			OsDriveSizeIsEnabled = true;
			OsSetupMessageIsVisible = true;

			OsDriveSettingsIsVisible = true;
		}
		else
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 25;
		
			CpuArchitecture = CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = BootMode.Uefi;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Disk;
			OsDriveTypeIsEnabled = false;
			
			OsDriveSizeIsEnabled = true;
			OsSetupMessageIsVisible = true;

			OsDriveSettingsIsVisible = true;
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
		RamSizeValid = RamSizeMiB != null && RamSizeMiB > 0 && RamSizeMiB <= SharedDefinitions.VmRamSizeMbMax;
		
		if ((OsDriveSize == null || OsDriveSize > _osDriveSizeMax || OsDriveSize < _osDriveSizeMin) && OperatingSystem != OperatingSystem.Other)
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
		
		CreateVmButtonIsEnabled = _driveService.IsInitialized && CheckCreationPermissions() && isVmNameValid && RamSizeValid && OsDriveSize != null && 
		                          ((OsDriveSize >= _osDriveSizeMin && OsDriveSize <= _osDriveSizeMax) || OperatingSystem == OperatingSystem.Other);
	}

	/// <summary>
	/// Checks whether the currently logged-in user has permissions to create a virtual machine with the current settings.
	/// </summary>
	/// <returns>True if the user has permissions, false otherwise.</returns>
	/// <remarks>
	/// Precondition: Checking user permissions is needed. Either user data has changed, or VM creation settings have changed. <br/>
	/// Postcondition: If the user has appropriate permissions, true is returned.
	/// If the user does not have appropriate permissions, false is returned and error messages are displayed.
	/// </remarks>
	private bool CheckCreationPermissions()
	{
		if (!ClientSvc.IsLoggedInAsSubUser)
		{
			VmCreationMessage = string.Empty;
			return true;
		}

		if (ClientSvc.User is not SubUser subUser)
			return false;

		if (!subUser.OwnerPermissions.HasPermission(
			    (UserPermissions.VirtualMachineCreate | UserPermissions.DriveCreate | UserPermissions.DriveConnect)
			    .AddIncluded()) && OperatingSystem != OperatingSystem.Other)
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessageErrorClass = true;
			VmCreationMessage = "You don't have permissions to create a virtual machine with an OS drive.";
			return false;
		}

		if (!subUser.OwnerPermissions.HasPermission(UserPermissions.VirtualMachineCreate.AddIncluded()))
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessageErrorClass = true;
			VmCreationMessage = "You don't have permissions to create a virtual machine.";
			return false;
		}

		return true;
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
		if (RamSizeMiB == null || OsDriveSize == null)
			return;
		
		string vmNameTrimmed = VmName.Trim();

		CreateVmButtonIsEnabled = false;
		VmCreationMessageErrorClass = false;
		VmCreationMessageSuccessClass = false;
		VmCreationMessage = "Creating the virtual machine - hang tight..";
		
		bool success;
		if (OperatingSystem == OperatingSystem.Other)
		{
			MessageResponseCreateVm? response = await ClientSvc.CreateVirtualMachineAsync(vmNameTrimmed, OperatingSystem, CpuArchitecture, RamSizeMiB.Value, BootMode);
			success = response != null && response.Result == MessageResponseCreateVm.Status.Success;
		}
		else
		{
			ExitCode result = await CreateVirtualMachineWithDriveAsync(vmNameTrimmed, OperatingSystem, CpuArchitecture, RamSizeMiB.Value, BootMode, OsDriveSize.Value);
			success = result == ExitCode.Success;
		}

		if (success)
		{
			VmCreationMessageSuccessClass = true;
			VmCreationMessageErrorClass = false;
			VmCreationMessage = "The virtual machine has been created successfully!";
		}
		else
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessageErrorClass = true;
			VmCreationMessage = "Could not create the virtual machine.";
			CreateVmButtonIsEnabled = true;
		}
	}

	/// <summary>
	/// Creates a virtual machine and an OS drive with the given settings.
	/// </summary>
	/// <param name="name">A name for the new virtual machine. Must be unique for the user. name != null.</param>
	/// <param name="operatingSystem">The operating system the virtual machine will run. operatingSystem != OperatingSystem.Other.</param>
	/// <param name="cpuArchitecture">The CPU architecture for the virtual machine's CPU.</param>
	/// <param name="ramSizeMiB">The amount of RAM the virtual machine will use while running. Must be in valid range according to the given operating system. ramSizeMiB >= 1.</param>
	/// <param name="bootMode">The boot mode the virtual machine will boot in. (BIOS, UEFI)</param>
	/// <param name="driveSizeMiB">The size of the OS drive for the virtual machine, in MiB. Must be in valid range according to the given operating system. driveSizeMiB >= 1.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The user has appropriate permissions for VM creation, drive creation, and drive connection. The given VM name is unique for the user.
	/// name != null &amp;&amp; operatingSystem != OperatingSystem.Other &amp;&amp; ramSizeMiB >= 1 &amp;&amp; driveSizeMiB >= 1. <br/>
	/// Postcondition: On success, the virtual machine and the OS drive are created and connected, and the returned exit code indicates success. <br/>
	/// If either operation (VM creation, drive creation, drive connection to VM) fails,
	/// an attempt to undo the succeeded operations is performed, and the returned exit code indicates the error.
	/// </remarks>
	private async Task<ExitCode> CreateVirtualMachineWithDriveAsync(string name, OperatingSystem operatingSystem,
		CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode, int driveSizeMiB)
	{
		if (string.IsNullOrEmpty(name) || operatingSystem == OperatingSystem.Other || ramSizeMiB < 1 || ramSizeMiB > SharedDefinitions.VmRamSizeMbMax ||
		    !Common.IsOperatingSystemDriveSizeValid(operatingSystem, driveSizeMiB))
		{
			return ExitCode.InvalidParameter;
		}
		
		string osDriveNameExtenstion = $" - {operatingSystem.ToString()}";
		string driveName = name.Substring(0,
			Math.Min(name.Length, SharedDefinitions.CredentialsMaxLength - osDriveNameExtenstion.Length)
		) + osDriveNameExtenstion;

		MessageResponseCreateDriveOs? driveRes = await ClientSvc.CreateDriveOsAsync(driveName, driveSizeMiB, operatingSystem);
		if (driveRes == null)
			return ExitCode.MessageFailure;

		if (driveRes.Result == MessageResponseCreateDriveOs.Status.Failure)
			return ExitCode.DriveCreationFailed;
	
		/* Search for an available drive name and use it. */
		int cnt = 0;
		while (driveRes.Result == MessageResponseCreateDriveOs.Status.DriveAlreadyExists)
		{
			osDriveNameExtenstion = $" - {operatingSystem.ToString()}_{cnt++}";

			driveName = name.Substring(0,
				Math.Min(name.Length,
					SharedDefinitions.CredentialsMaxLength - osDriveNameExtenstion.Length)
			) + osDriveNameExtenstion;

			driveRes = await ClientSvc.CreateDriveOsAsync(driveName, OsDriveSize!.Value, operatingSystem);
			if (driveRes == null)
				return ExitCode.MessageFailure;

			if (driveRes.Result == MessageResponseCreateDriveOs.Status.Failure)
				return ExitCode.DriveCreationFailed;		
		}
		
		/* Drive created successfully. */
		
		MessageResponseCreateVm? vmCreateRes = await ClientSvc.CreateVirtualMachineAsync(name, operatingSystem, cpuArchitecture, ramSizeMiB, bootMode);
		ExitCode result = ExitCode.Success;
		
		if (vmCreateRes == null)
			result = ExitCode.MessageFailure;
		
		else if (vmCreateRes.Result == MessageResponseCreateVm.Status.Failure)
			result = ExitCode.VmCreationFailed;

		if (result == ExitCode.Success)
			result = await _driveService.ConnectDriveAsync(driveRes.DriveId, vmCreateRes!.VmId);

		if (result != ExitCode.Success)
		{
			List<Task> tasks = new List<Task>();
			tasks.Add(ClientSvc.DeleteItemAsync(driveRes.DriveId, string.Empty));
			
			if (vmCreateRes != null && vmCreateRes.Result == MessageResponseCreateVm.Status.Success)
				tasks.Add(ClientSvc.DeleteVirtualMachineAsync(vmCreateRes.VmId));
			
			await Task.WhenAll(tasks);
		}
		
		return result;
	}
}