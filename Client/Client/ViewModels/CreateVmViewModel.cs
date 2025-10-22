using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;

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
	private SharedDefinitions.OperatingSystem _operatingSystem = SharedDefinitions.OperatingSystem.Ubuntu;
	
	[ObservableProperty]
	private bool _osSetupMessageIsVisible = true;
	
	[ObservableProperty]
	private SharedDefinitions.CpuArchitecture _cpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
	
	[ObservableProperty]
	private bool _cpuArchitectureIsEnabled = true;

	[ObservableProperty]
	private SharedDefinitions.BootMode _bootMode = SharedDefinitions.BootMode.Uefi;
	
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
		_driveService = driveService;
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
			_osDriveSizeMax = 20;
			_osDriveSizeMin = 9;
			
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86;
			CpuArchitectureIsEnabled = false;
		
			BootMode = SharedDefinitions.BootMode.Bios;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Floppy;
			OsDriveTypeIsEnabled = false;
			OsDriveSizeIsEnabled = true;

			OsSetupMessageIsVisible = false;

			OsDriveSettingsIsVisible = true;
		}
		else if (OperatingSystem == SharedDefinitions.OperatingSystem.Other)
		{
			CpuArchitectureIsEnabled = true;
			BootModeIsEnabled = true;
			
			OsDriveTypeIsEnabled = false;
			OsDriveSizeIsEnabled = false;
			OsSetupMessageIsVisible = false;
			OsDriveSettingsIsVisible = false;
		}
		else if (OperatingSystem == SharedDefinitions.OperatingSystem.Ubuntu)
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 25;
		
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = SharedDefinitions.BootMode.Uefi;
			BootModeIsEnabled = false;
			
			OsDriveType = DriveType.Disk;
			OsDriveTypeIsEnabled = false;
			
			OsDriveSizeIsEnabled = true;
			OsSetupMessageIsVisible = true;

			OsDriveSettingsIsVisible = true;
		}
		else if (OperatingSystem == SharedDefinitions.OperatingSystem.ManjaroLinux)
		{
			_osDriveSizeMax = 1024 * 256;
			_osDriveSizeMin = 1024 * 30;
		
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = SharedDefinitions.BootMode.Uefi;
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
		
			CpuArchitecture = SharedDefinitions.CpuArchitecture.X86_64;
			CpuArchitectureIsEnabled = false;
			
			BootMode = SharedDefinitions.BootMode.Uefi;
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
		
		CreateVmButtonIsEnabled = _driveService.IsInitialized && isVmNameValid && OsDriveSize != null && 
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
		string vmNameTrimmed = VmName.Trim();
		bool createDrive = OperatingSystem != SharedDefinitions.OperatingSystem.Other;
		Task<MessageResponseCreateVm?> taskCreateVm = ClientSvc.CreateVirtualMachineAsync(vmNameTrimmed, OperatingSystem, CpuArchitecture, BootMode);
		Task<MessageResponseCreateDrive?> taskCreateDrive = null!;
		
		MessageResponseCreateVm.Status createVmResult = MessageResponseCreateVm.Status.Failure;
		MessageResponseCreateDrive.Status createDriveResult = MessageResponseCreateDrive.Status.Failure;
		ExitCode connectDriveResult = ExitCode.Failure;

		if (!createDrive)
		{
			await taskCreateVm;
		}
		else
		{
			string driveName = $"{vmNameTrimmed} - {OperatingSystem.ToString()}";
			
			taskCreateDrive = ClientSvc.CreateDriveAsync(driveName, OsDriveType, OsDriveSize!.Value, OperatingSystem);
			await Task.WhenAll(taskCreateVm, taskCreateDrive);

			createVmResult = taskCreateVm.Result == null
				? MessageResponseCreateVm.Status.Failure
				: taskCreateVm.Result.Result;
			
			createDriveResult = taskCreateDrive.Result == null 
				? MessageResponseCreateDrive.Status.Failure 
				: taskCreateDrive.Result.Result;

			if (createVmResult == MessageResponseCreateVm.Status.Success)
			{
				/* Search for an available drive name and use it. */
				int cnt = 0;
				while (createDriveResult == MessageResponseCreateDrive.Status.DriveAlreadyExists)
				{
					taskCreateDrive = ClientSvc.CreateDriveAsync(driveName + "_" + cnt++, OsDriveType,
						OsDriveSize!.Value, OperatingSystem);

					await taskCreateDrive;

					createDriveResult = taskCreateDrive.Result == null
						? MessageResponseCreateDrive.Status.Failure
						: taskCreateDrive.Result.Result;
				}

				if (createDriveResult == MessageResponseCreateDrive.Status.Success)
				{
					connectDriveResult = await _driveService.ConnectDriveAsync(taskCreateDrive.Result!.DriveId, taskCreateVm.Result!.VmId);
				}
			}
		}
	
		/* If Other is selected as the operating system - taskCreateDrive wont have a value because we are not creating a drive. */
		if (createVmResult == MessageResponseCreateVm.Status.Success && 
		    (!createDrive || (createDriveResult == MessageResponseCreateDrive.Status.Success && connectDriveResult == ExitCode.Success)))
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
			    createDriveResult != MessageResponseCreateDrive.Status.Success ||
			    connectDriveResult != ExitCode.Success)
		   )
		{
			/* One or more of the operations has failed - delete the ones that have succeeded. */
			
			List<Task> tasks = new List<Task>();
			if (connectDriveResult == ExitCode.Success)
			{
				/* TODO: Delete the drive connection */
			}
			
			if (createDriveResult == MessageResponseCreateDrive.Status.Success)
			{
				tasks.Add(ClientSvc.DeleteDriveAsync(taskCreateDrive.Result!.DriveId));
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