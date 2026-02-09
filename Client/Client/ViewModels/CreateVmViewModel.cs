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
			VmCreationMessage = "You don't have permissions to create a virtual machine with an OS drive.";
			return false;
		}

		if (!subUser.OwnerPermissions.HasPermission(UserPermissions.VirtualMachineCreate.AddIncluded()))
		{
			VmCreationMessageSuccessClass = false;
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
		string vmNameTrimmed = VmName.Trim();
		bool createDrive = OperatingSystem != OperatingSystem.Other;
		Task<MessageResponseCreateVm?> taskCreateVm = ClientSvc.CreateVirtualMachineAsync(vmNameTrimmed, OperatingSystem, CpuArchitecture, RamSizeMiB!.Value, BootMode);
		Task<MessageResponseCreateDriveOs?> taskCreateDrive = null!;
		
		MessageResponseCreateVm.Status createVmResult = MessageResponseCreateVm.Status.Failure;
		MessageResponseCreateDriveOs.Status createDriveResult = MessageResponseCreateDriveOs.Status.Failure;
		ExitCode connectDriveResult = ExitCode.Failure;

		if (!createDrive)
		{
			await taskCreateVm;
			
			createVmResult = taskCreateVm.Result?.Result ?? MessageResponseCreateVm.Status.Failure;
		}
		else
		{
			string osDriveNameExtenstion = $" - {OperatingSystem.ToString()}";
			string driveName = vmNameTrimmed.Substring(0,
				Math.Min(vmNameTrimmed.Length, SharedDefinitions.CredentialsMaxLength - osDriveNameExtenstion.Length)
			) + osDriveNameExtenstion;
			
			taskCreateDrive = ClientSvc.CreateDriveOsAsync(driveName, OsDriveSize!.Value, OperatingSystem);
			await Task.WhenAll(taskCreateVm, taskCreateDrive);

			createVmResult = taskCreateVm.Result?.Result ?? MessageResponseCreateVm.Status.Failure;
			
			createDriveResult = taskCreateDrive.Result?.Result ?? MessageResponseCreateDriveOs.Status.Failure;

			if (createVmResult == MessageResponseCreateVm.Status.Success)
			{
				/* Search for an available drive name and use it. */
				int cnt = 0;
				while (createDriveResult == MessageResponseCreateDriveOs.Status.DriveAlreadyExists)
				{
					osDriveNameExtenstion = $" - {OperatingSystem.ToString()}_{cnt++}";
					
					driveName = vmNameTrimmed.Substring(0,
						Math.Min(vmNameTrimmed.Length, SharedDefinitions.CredentialsMaxLength - osDriveNameExtenstion.Length)
					) + osDriveNameExtenstion;
					
					taskCreateDrive = ClientSvc.CreateDriveOsAsync(driveName, OsDriveSize!.Value, OperatingSystem);

					await taskCreateDrive;

					createDriveResult = taskCreateDrive.Result == null
						? MessageResponseCreateDriveOs.Status.Failure
						: taskCreateDrive.Result.Result;
				}

				if (createDriveResult == MessageResponseCreateDriveOs.Status.Success)
				{
					connectDriveResult = await _driveService.ConnectDriveAsync(taskCreateDrive.Result!.DriveId, taskCreateVm.Result!.VmId);
				}
			}
		}
	
		/* If Other is selected as the operating system - taskCreateDrive wont have a value because we are not creating a drive. */
		if (createVmResult == MessageResponseCreateVm.Status.Success && 
		    (!createDrive || (createDriveResult == MessageResponseCreateDriveOs.Status.Success && connectDriveResult == ExitCode.Success)))
		{	
			VmCreationMessageSuccessClass = true;
			VmCreationMessage = "The virtual machine has been created successfully!";
		} 
		else if (createVmResult == MessageResponseCreateVm.Status.VmAlreadyExists)
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessage = $"A virtual machine called \"{vmNameTrimmed}\" already exists.";
		}
		else
		{
			VmCreationMessageSuccessClass = false;
			VmCreationMessage = "Could not create the virtual machine.";
		}

		/* If something went wrong */
		if (createDrive && (
			    createVmResult != MessageResponseCreateVm.Status.Success ||
			    createDriveResult != MessageResponseCreateDriveOs.Status.Success ||
			    connectDriveResult != ExitCode.Success)
		   )
		{
			/* One or more of the operations has failed - delete the ones that have succeeded. */
			
			List<Task> tasks = new List<Task>();
			if (connectDriveResult == ExitCode.Success)
			{
				tasks.Add(_driveService.DisconnectDriveAsync(taskCreateDrive.Result!.DriveId, taskCreateVm.Result!.VmId));		/* Both should be valid. (because connected) */
			}
			
			if (createDriveResult == MessageResponseCreateDriveOs.Status.Success)
			{
				tasks.Add(ClientSvc.DeleteItemAsync(taskCreateDrive.Result!.DriveId, string.Empty));
			}
			
			if (createVmResult == MessageResponseCreateVm.Status.Success)
			{
				tasks.Add(ClientSvc.DeleteVirtualMachineAsync(taskCreateVm.Result!.VmId));
			}
			
			await Task.WhenAll(tasks);
		}

		CreateVmButtonIsEnabled = false;
	}
}