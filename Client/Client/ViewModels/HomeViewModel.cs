using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Client.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
	public event EventHandler<VmGeneralDescriptor>? VmOpenClicked;
	public ObservableCollection<VmItemTemplate> Vms { get; }

	private readonly DriveService _driveService;
	
	private VmItemTemplate _forceOffWarningVm = null!;

	public bool VmsUsable { get; } = true;
	
	[ObservableProperty] 
	private bool _forceOffWarningIsOpen = false;
	
	[ObservableProperty]
	private string _forceOffWarningQuestion = string.Empty;

	[ObservableProperty]
	private bool _deleteVmPopupIsOpen = false;

	[ObservableProperty] 
	private bool _deleteVmPopupHasDrives = false;

	[ObservableProperty] 
	private bool _deleteVmPopupVmRunning;
	
	public ObservableCollection<DeletionDriveItemTemplate> DeleteVmPopupDrives { get; set; }		/* Drives the user can select to delete, when deleting a VM */

	private VmGeneralDescriptor _deleteVmPopupVmDescriptor = null!;

	[ObservableProperty] 
	private bool _conPopupIsOpen = false;
	
	private int _conPopupVmId = -1;
	
	public ObservableCollection<DriveConnectionItemTemplate> ConPopupDriveConnections { get; }
	
	/// <summary>
	/// Initializes a new instance of HomeViewModel.
	/// </summary>
	/// <param name="navigationSvc">The navigation service. navigationSvc != null.</param>
	/// <param name="clientSvc">The client service. clientSvc != null.</param>
	/// <param name="driveService">The drive service. driveService != null.</param>
	/// <remarks>
	/// Precondition: MainView is created, (HomeViewModel is the default side menu selection) or the user selects the home page in the side menu. <br/>
	/// navigationService != null &amp;&amp; clientSvc != null &amp;&amp; driveService != null <br/>
	/// Postcondition: A new instance of HomeViewModel is created.
	/// </remarks>
	public HomeViewModel(NavigationService navigationSvc, ClientService clientSvc, DriveService driveService)
		: base(navigationSvc, clientSvc)
	{
		Vms = new ObservableCollection<VmItemTemplate>();
		DeleteVmPopupDrives = new ObservableCollection<DeletionDriveItemTemplate>();
		ConPopupDriveConnections = new ObservableCollection<DriveConnectionItemTemplate>();
		_driveService = driveService;
		
		if (ClientSvc.IsLoggedInAsSubUser && ClientSvc.User is SubUser subUser)
		{
			VmsUsable = subUser.OwnerPermissions.HasPermission(UserPermissions.VirtualMachineUse);
			DriveConnectionItemTemplate.CanConnect = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveConnect);
			DriveConnectionItemTemplate.CanDisconnect = subUser.OwnerPermissions.HasPermission(UserPermissions.DriveDisconnect);
		}
		else
		{
			DriveConnectionItemTemplate.CanConnect = true;
			DriveConnectionItemTemplate.CanDisconnect = true;
		}
		
		_driveService.Initialized += (sender, code) =>
		{
			if (code == ExitCode.Success) 
				_ = InitializeAsync();
		};
		ClientSvc.VmCreated += OnVmCreated;
		ClientSvc.VmDeleted += OnVmDeleted;
		ClientSvc.VmPoweredOn += OnVmPoweredOn;
		ClientSvc.VmPoweredOff += OnVmPoweredOffOrCrashed;
		ClientSvc.VmCrashed += OnVmPoweredOffOrCrashed;
		ClientSvc.ItemDeleted += OnItemDeleted;
		ClientSvc.DriveConnected += OnDriveConnected;
	}

	/* Use for IDE preview only. */
	public HomeViewModel()
	{
		_driveService = null!;
		Vms = new ObservableCollection<VmItemTemplate>()
		{
			new VmItemTemplate(1, "test_vm0", OperatingSystem.Ubuntu, VmState.ShutDown, 8192),
			new VmItemTemplate(2, "test_vm1", OperatingSystem.ManjaroLinux, VmState.ShutDown, 4096),
			new VmItemTemplate(3, "test_vm2", OperatingSystem.MiniCoffeeOS, VmState.ShutDown, 5),
		};
		DeleteVmPopupDrives = new ObservableCollection<DeletionDriveItemTemplate>();
		ConPopupDriveConnections = new ObservableCollection<DriveConnectionItemTemplate>();
	}
	
	/// <summary>
	/// Initializes HomeViewModel. Fetches virtual machines and displays them.
	/// </summary>
	/// <remarks>
	/// Precondition: Drive service (_driveService) was initialized successfully. <br/>
	/// Postcondition: The virtual machines of the user (if any) are displayed.
	/// </remarks>
	private async Task InitializeAsync()
	{
		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			Vms.Clear();
			
			VmGeneralDescriptor[] vms = _driveService.GetVirtualMachines();
			foreach (VmGeneralDescriptor vm in vms)
			{
				VmItemTemplate template = new VmItemTemplate(ClientSvc, vm);

				template.OpenClicked += OnVmOpenClicked;
				template.ForceOffClicked += OnVmForceOffClicked;
				template.ManageDriveConnectionsClicked += OnManageDriveConnectionsClick;
				template.DeleteClicked += OnVmDeleteClicked;
				
				Vms.Add(template);
			}
		});
	}

	/// <summary>
	/// Handles the event that a new virtual machine was created. Adds the new virtual machine to the displayed virtual machines.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="descriptor">The descriptor of the new virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: A new virtual machine was created. descriptor != null. <br/>
	/// Postcondition: An operation of adding the virtual machine to the displayed virtual machines is posted unto the UI thread. Will execute soon.
	/// </remarks>
	private void OnVmCreated(object? sender, VmGeneralDescriptor descriptor)
	{
		Dispatcher.UIThread.Post(() =>
		{
			VmItemTemplate template = new VmItemTemplate(ClientSvc, descriptor);

			template.OpenClicked += OnVmOpenClicked;
			template.ForceOffClicked += OnVmForceOffClicked;
			template.ManageDriveConnectionsClicked += OnManageDriveConnectionsClick;
			template.DeleteClicked += OnVmDeleteClicked;
				
			Vms.Add(template);
		});
	}
	
	/// <summary>
	/// Handles the event that a virtual machine was deleted.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine that was deleted. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was deleted. vmId >= 1. <br/>
	/// Postcondition: UI is updated accordingly.
	/// </remarks>
	private void OnVmDeleted(object? sender, int vmId)
	{
		if (vmId < 1) return;
		
		Dispatcher.UIThread.Post(() =>
		{
			if (DeleteVmPopupIsOpen)
			{
				if (_deleteVmPopupVmDescriptor.Id == vmId)
				{
					DeletePopupClosed();
				}
				else
				{
					DeleteVmPopupInitialize(_deleteVmPopupVmDescriptor);
				}
			}

			int vmIndex = -1;
			for (int i = 0; i < Vms.Count; ++i)
			{
				if (Vms[i].Id == vmId) vmIndex = i;
			}

			if (vmIndex != -1)
			{
				Vms.RemoveAt(vmIndex);
			}
		});
	}

	/// <summary>
	/// Handles the event that a virtual machine was powered on.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine that was powered on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered on. vmId >= 1. <br/>
	/// Postcondition: State change is handled.
	/// </remarks>
	private void OnVmPoweredOn(object? sender, int vmId)
	{
		if (vmId < 1) return;
		
		OnVmStateChanged(vmId, VmState.Running);
	}
	
	/// <summary>
	/// Handles the event that a virtual machine was powered off or has crashed.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="vmId">The ID of the virtual machine of which the state has changed. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered off or has crashed. vmId >= 1. <br/>
	/// Postcondition: State change is handled.
	/// </remarks>
	private void OnVmPoweredOffOrCrashed(object? sender, int vmId)
	{
		if (vmId < 1) return;
		
		OnVmStateChanged(vmId, VmState.ShutDown);
	}

	/// <summary>
	/// Handles an item deleted event.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="info">The item deletion info. info != null.</param>
	/// <remarks>
	/// Precondition: An item was deleted. info != null. <br/>
	/// Postcondition: Event is handled, UI informed if needed.
	/// </remarks>
	private void OnItemDeleted(object? sender, MessageInfoItemDeleted info)
	{
		if (info.DriveId < 1 || !Common.IsPathToDrive(info.Path)) 
			return;

		if (DeleteVmPopupIsOpen)
		{
			Dispatcher.UIThread.Post(() => DeleteVmPopupInitialize(_deleteVmPopupVmDescriptor));
		}
	}

	/// <summary>
	/// Handles the event that a drive was connected to a virtual machine. (New connection created)
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="connection">The new drive-VM connection. connection != null.</param>
	/// <remarks>
	/// Precondition: A drive was connected to a virtual machine. (New drive-VM connection added) connection != null. <br/>
	/// Postcondition: Event is handled, UI informed if needed.
	/// </remarks>
	private void OnDriveConnected(object? sender, DriveConnection connection)
	{
		if (DeleteVmPopupIsOpen)
		{
			Dispatcher.UIThread.Post(() => DeleteVmPopupInitialize(_deleteVmPopupVmDescriptor));
		}
	}
	
	/// <summary>
	/// Handles a change in the state of a virtual machine. (powered on/off, crashed)
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine of which the state has changed. vmId >= 1.</param>
	/// <param name="newState">The new state of the virtual machine.</param>
	/// <remarks>
	/// Precondition: The state of the given virtual machine has changed. (powered on/off, crashed) vmId >= 1. <br/>
	/// Postcondition: State change is handled.
	/// </remarks>
	private void OnVmStateChanged(int vmId, VmState newState)
	{
		if (vmId < 1) return;
		
		if (DeleteVmPopupIsOpen)
		{
			if (_deleteVmPopupVmDescriptor.Id == vmId)
			{
				_deleteVmPopupVmDescriptor.State = newState;
			}
			
			Dispatcher.UIThread.Post(() => DeleteVmPopupInitialize(_deleteVmPopupVmDescriptor));
		}	
	}
	
	/// <summary>
	/// Handles a click on the Open button of one of the users VMs. Open a new tab for the VM. If a tab exists, redirect the user to it.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the Open button on a VM. <br/>
	/// Postcondition: A new tab is opened for the VM. If a tab for the VM is already open, the user will be redirected to it.
	/// </remarks>
	private void OnVmOpenClicked(VmGeneralDescriptor descriptor) =>
		VmOpenClicked?.Invoke(this, descriptor);

	/// <summary>
	/// Handles a click on one of the VM's force off button. Displays the force off warning message.
	/// </summary>
	/// <param name="template">The virtual machine that the force off button was clicked on. template != null.</param>
	/// <remarks>
	/// Precondition: User has clicked the force off button on a virtual machine. template != null. <br/>
	/// Postcondition: The force off warning popup is displayed.
	/// </remarks>
	private void OnVmForceOffClicked(VmItemTemplate template)
	{
		_forceOffWarningVm = template;
		ForceOffWarningQuestion = $"Are you sure you want to force off {template.Name}?";
		ForceOffWarningIsOpen = true;
	}

	/// <summary>
	/// Handles a click on one of the VM's delete button. Displays a confirmation popup.
	/// </summary>
	/// <param name="vm">A descriptor of the virtual machine that the delete button was clicked upon. vm != null.</param>
	/// <remarks>
	/// Precondition: User has clicked on the delete button on a virtual machine. vm != null.<br/>
	/// Postcondition: A confirmation popup appears.
	/// </remarks>
	private void OnVmDeleteClicked(VmGeneralDescriptor vm) => DeleteVmPopupInitialize(vm);

	/// <summary>
	/// Initializes the delete virtual machine popup.
	/// </summary>
	/// <param name="descriptor">A descriptor of the virtual machine that the delete button was clicked upon. descriptor != null</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Initializing the VM delete popup is needed. (Either popup just opened, or the state of drives has changed) descriptor != null. <br/>
	/// Postcondition: On success, the popup is initialized and messages are displayed, the returned exit code indicates success. <br/>
	/// On failure, the popup is closed and the returned exit code indicates the error.
	/// </remarks>
	private ExitCode DeleteVmPopupInitialize(VmGeneralDescriptor descriptor)
	{
		if (descriptor.Id < 1) return ExitCode.InvalidParameter;
		
		_deleteVmPopupVmDescriptor = descriptor;

		DeleteVmPopupVmRunning = _deleteVmPopupVmDescriptor.State == VmState.Running;
		
		DriveGeneralDescriptor[]? drives = _driveService.GetDrivesOnVirtualMachine(descriptor.Id);

		DeleteVmPopupDrives.Clear();
		DeleteVmPopupHasDrives = drives != null && drives.Length != 0;

		if (drives != null)
		{
			foreach (DriveGeneralDescriptor drive in drives)
			{
				bool driveInUse = _driveService.IsDriveInUse(drive.Id);
				DeleteVmPopupDrives.Add(new DeletionDriveItemTemplate(drive.Id, drive.Name, drive.DriveType, drive.Size,
					driveInUse));
			}
		}

		DeleteVmPopupIsOpen = true;	
		
		return ExitCode.Success;
	}
	
	/// <summary>
	/// Called to close the force off warning popup, or called when it is closed, or if the user clicks on either the Cancel or Force Off buttons on the popup. Cleans up.
	/// </summary>
	/// <remarks>
	/// Precondition: User has closed the force off warning popup. <br/>
	/// Postcondition: Popup is closed. Resources are freed.
	/// </remarks>
	[RelayCommand]
	private void ForceOffWarningClosed()
	{
		ForceOffWarningIsOpen = false;
		ForceOffWarningQuestion = string.Empty;
	}

	/// <summary>
	/// Handles a click on the force off confirmation button. (The one on the popup itself)
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the force off confirmation button. (The one on the popup itself) <br/>
	/// Postcondition: A force off is requested. On success (or if VM already powered off), the virtual machine is powered off. On failure, an error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task ForceOffConfirmClickAsync()
	{
		MessageResponseVmForceOff.Status result = await ClientSvc.ForceOffVirtualMachineAsync(_forceOffWarningVm.Id);
		ForceOffWarningClosed();

		if (result == MessageResponseVmForceOff.Status.Failure)
		{
			_forceOffWarningVm.ErrorMessage = "Force off failed.";
		}
	}

	/// <summary>
	/// Closes the VM delete confirmation popup. Also called when the popup closes.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the popup, or closing it is needed. Popup is open. <br/>
	/// Postcondition: Popup is closed.
	/// </remarks>
	[RelayCommand]
	private void DeletePopupClosed()
	{
		DeleteVmPopupIsOpen = false;
		DeleteVmPopupDrives.Clear();
	}

	/// <summary>
	/// Handles a click on the delete VM confirmation button. (the one on the delete VM popup)
	/// Attempts to delete the virtual machine and the selected drives.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the delete VM confirmation button. (the one on the delete VM popup)
	/// Postcondition: On success, the virtual machine and the selected drives are deleted. <br/>
	/// If the virtual machine could not be deleted, the selected drives are not deleted.
	/// If the virtual machine was deleted successfully, each drive marked for deletion will attempt deletion.
	/// </remarks>
	[RelayCommand]
	private async Task DeleteVmConfirmDeleteClickAsync()
	{
		MessageResponseDeleteVm.Status deleteVmResult = await ClientSvc.DeleteVirtualMachineAsync(_deleteVmPopupVmDescriptor.Id);

		if (deleteVmResult == MessageResponseDeleteVm.Status.Success)
		{
			List<Task<MessageResponseDeleteItem.Status>> deleteDriveTasks = new List<Task<MessageResponseDeleteItem.Status>>();
			foreach (DeletionDriveItemTemplate drive in DeleteVmPopupDrives)
			{
				if (drive.IsMarkedForDeletion)
				{
					deleteDriveTasks.Add(ClientSvc.DeleteItemAsync(drive.Id, string.Empty));
				}
			}
		
			await Task.WhenAll(deleteDriveTasks);
			
			DeletePopupClosed();
		}
		else if (deleteVmResult == MessageResponseDeleteVm.Status.VirtualMachineIsRunning)
		{
			_deleteVmPopupVmDescriptor.State = VmState.Running;
			DeleteVmPopupInitialize(_deleteVmPopupVmDescriptor);
		}
	}

	/// <summary>
	/// Either closes the VM-drive connection popup, or called after it is closed.
	/// </summary>
	/// <remarks>
	/// Precondition: Either the user has closed the popup, or this method was called in order to close the popup. <br/>
	/// Postcondition: VM-drive connection management popup is closed.
	/// </remarks>
	[RelayCommand]
	private void CloseConPopup()
	{
		ConPopupIsOpen = false;
		ConPopupDriveConnections.Clear();
	}

	/// <summary>
	/// Handles a click on the VM-drive connections management button on a virtual machine. Opens the VM-drive connection management popup.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that the VM-drive connections management button was clicked on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: User has clicked on the VM-drive connections management button on a virtual machine. vmId >= 1. <br/>
	/// Postcondition: The VM-drive connections management popup is open.
	/// </remarks>
	private void OnManageDriveConnectionsClick(int vmId)
	{
		_conPopupVmId = vmId;

		DriveGeneralDescriptor[] drives = _driveService.GetDrives();
		foreach (DriveGeneralDescriptor drive in drives)
		{
			bool connected = _driveService.ConnectionExists(drive.Id, vmId);
			ConPopupDriveConnections.Add(new DriveConnectionItemTemplate(drive, connected));
		}
		
		ConPopupIsOpen = true;
	}
	
	/// <summary>
	/// Handles a click on the apply button on the VM-drive connection management popup. Attempts to apply the new changes.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the apply button on the VM-drive connection management popup. <br/>
	/// Postcondition: Each drive that its connection state to this virtual machine was changed (connected/disconnected), will be updated to the new state.
	/// On failure, the VM-drive connection will not change.
	/// </remarks>
	[RelayCommand]
	private async Task ConPopupApplyClickAsync()
	{
		List<Task<ExitCode>> connectionUpdates = new List<Task<ExitCode>>();
		foreach (DriveConnectionItemTemplate item in ConPopupDriveConnections)
		{
			bool isConnected = _driveService.ConnectionExists(item.Id, _conPopupVmId);
			if (item.IsChecked && !isConnected)
			{
				connectionUpdates.Add(_driveService.ConnectDriveAsync(item.Id, _conPopupVmId));
			}
			else if (!item.IsChecked && isConnected)
			{
				connectionUpdates.Add(_driveService.DisconnectDriveAsync(item.Id, _conPopupVmId));
			}
		}
		
		await Task.WhenAll(connectionUpdates);
		
		CloseConPopup();
	}
}

public partial class VmItemTemplate : ObservableObject
{
	public Action<VmGeneralDescriptor>? OpenClicked;
	public Action<VmItemTemplate>? ForceOffClicked;
	public Action<int>? ManageDriveConnectionsClicked;
	public Action<VmGeneralDescriptor>? DeleteClicked;
	
	public int Id { get; }

	[ObservableProperty] 
	private string _name;

	private OperatingSystem _operatingSystem;
	public OperatingSystem OperatingSystem
	{
		get => _operatingSystem;
		private set
		{
			_operatingSystem = value;
			OperatingSystemString = _operatingSystem == OperatingSystem.Other
				? "Unknown OS"
				: Common.SeparateStringWords(_operatingSystem.ToString());
		}
	}
	private CpuArchitecture _cpuArchitecture;
	
	private readonly ClientService _clientService;

	[ObservableProperty]
	private string _operatingSystemString = string.Empty;

	private VmState _state;
	public VmState State
	{
		get => _state;
		set
		{
			_state = value;
			StateString = Common.SeparateStringWords(_state.ToString());
			if (_state == VmState.ShutDown)
			{
				StateColor = new SolidColorBrush(Color.FromRgb(0x4F, 0x5B, 0x5B));
			}
			else if (_state == VmState.Running)
			{
				StateColor = new SolidColorBrush(Color.FromRgb(0x6B, 0xE5, 0x78));
			}
		}
	}
	public int RamSizeMiB { get; }
	private BootMode _bootMode { get; }

	[ObservableProperty] 
	private string _stateString = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	[ObservableProperty]
	private bool _errorMessageIsVisible;

	[ObservableProperty] 
	private Brush _stateColor;

	partial void OnErrorMessageChanged(string value) => ErrorMessageIsVisible = !string.IsNullOrEmpty(value);

	/// <summary>
	/// Creates a new instance of a VmItemTemplate.
	/// </summary>
	/// <param name="clientService">The client communication service. clientService != null.</param>
	/// <param name="descriptor">A descriptor of the virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: clientService != null &amp;&amp; descriptor != null. <br/>
	/// Postcondition: A new instance of VmItemTemplate is created.
	/// </remarks>
	public VmItemTemplate(ClientService clientService, VmGeneralDescriptor descriptor)
	{
		_clientService = clientService;
		Id = descriptor.Id;
		Name = descriptor.Name;
		OperatingSystem = descriptor.OperatingSystem;
		_cpuArchitecture = descriptor.CpuArchitecture;
		State = descriptor.State;
		RamSizeMiB = descriptor.RamSizeMiB;
		_bootMode = descriptor.BootMode;
		_clientService.VmPoweredOn += OnVmPoweredOn;
		_clientService.VmPoweredOff += OnVmPoweredOff;
		_clientService.VmCrashed += OnVmCrashed;
	}
	
	/* Use for IDE preview only. */
	public VmItemTemplate(int id, string name, OperatingSystem operatingSystem, VmState state, int ramSizeMiB)
	{
		_clientService = null!;
		Id = id;
		Name = name;
		OperatingSystem = operatingSystem;
		State = state;
		RamSizeMiB = ramSizeMiB;
	}

	/// <summary>
	/// Returns a VM general descriptor of this VM item template.
	/// </summary>
	/// <returns>A VM general descriptor of this VM item template.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: A VM general descriptor of this VM item template is returned.
	/// </remarks>
	public VmGeneralDescriptor AsVmGeneralDescriptor() => 
		new VmGeneralDescriptor(Id, Name, OperatingSystem, _cpuArchitecture, State, RamSizeMiB, _bootMode);
	
	/// <summary>
	/// Handles the event of the virtual machine being powered on.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered on. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine was powered on. id >= 1. <br/>
	/// Postcondition: Event is handled, UI updates accordingly.
	/// </remarks>
	private void OnVmPoweredOn(object? sender, int id)
	{
		if (Id != id)
		{
			return;
		}

		Dispatcher.UIThread.Post(() =>
		{
			State = VmState.Running;
		});
	}

	/// <summary>
	/// Handles the event of the virtual machine being powered off.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered off. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine was powered off. id >= 1. <br/>
	/// Postcondition: Event is handled, UI updates accordingly.
	/// </remarks>
	private void OnVmPoweredOff(object? sender, int id)
	{
		if (Id != id)
		{
			return;
		}

		Dispatcher.UIThread.Post(() =>
		{
			State = VmState.ShutDown;
		});
	}
	
	/// <summary>
	/// Handles the event of the virtual machine crashing
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that has crashed. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine has crashed. id >= 1. <br/>
	/// Postcondition: Event is handled, UI updates accordingly.
	/// </remarks>
	private void OnVmCrashed(object? sender, int id)
	{
		if (Id != id)
		{
			return;
		}

		Dispatcher.UIThread.Post(() =>
		{
			State = VmState.ShutDown;
			ErrorMessage = "The VM has crashed.";
		});

	}

	/// <summary>
	/// Handles opening the VM - opens a tab for the VM.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the Open button on a VM. <br/>
	/// Postcondition: A new tab is opened for the VM. If a tab for the VM is already open, the user will be redirected to it.
	/// </remarks>
	[RelayCommand]
	private void OpenClick() => OpenClicked?.Invoke(AsVmGeneralDescriptor());

	/// <summary>
	/// Handles a click on the power on button. Attempts to power on the virtual machine.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the power on button in a VM. User logged in and has power permissions for the VM.<br/>
	/// Postcondition: On success, the VM is powered on. On failure, an error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task PowerOnClickAsync()
	{
		MessageResponseVmStartup.Status result = await _clientService.PowerOnVirtualMachineAsync(Id);
		ErrorMessage = string.Empty;
		if (result == MessageResponseVmStartup.Status.VmAlreadyRunning)
			State = VmState.Running;
	
		else if (result == MessageResponseVmStartup.Status.ServerStarvation)
			ErrorMessage = "Server under high load.";
		
		else if(result != MessageResponseVmStartup.Status.Success)
			ErrorMessage = "VM startup failed.";
	}

	/// <summary>
	/// Handles a click on the power off button. Attempts to power off the virtual machine.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the power off button. User is logged in and has power permissions for the virtual machine. <br/>
	/// Postcondition: On success, the virtual machine is powered off. On failure, an error message is displayed.
	/// </remarks>
	[RelayCommand]
	private async Task PowerOffClickAsync()
	{
		MessageResponseVmShutdown.Status result = await _clientService.PowerOffVirtualMachineAsync(Id);
		ErrorMessage = string.Empty;
		if (result == MessageResponseVmShutdown.Status.VmIsShutDown)
		{
			State = VmState.ShutDown;
		}
		else if (result != MessageResponseVmShutdown.Status.Success)
		{
			ErrorMessage = "VM shutdown failed.";
		}
	}

	/// <summary>
	/// Handles a click on the dismiss button on a VM error message. Hides the error message.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the dismiss button on a VM error message. <br/>
	/// Postcondition: Error message is hidden.
	/// </remarks>
	[RelayCommand]
	private void ErrorMessageDismiss() => ErrorMessage = string.Empty;

	/// <summary>
	/// Handles a click on the force off button.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the force off button on this virtual machine. <br/>
	/// Postcondition: Warning popup appears.
	/// </remarks>
	[RelayCommand]
	private void ForceOffClick() => ForceOffClicked?.Invoke(this);

	/// <summary>
	/// Handles a click on the manage drive connections button.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the manage drive connections button on this virtual machine. <br/>
	/// Postcondition: A drive connection management popup appears.
	/// </remarks>
	[RelayCommand]
	private void ManageDriveConnectionsClick() => ManageDriveConnectionsClicked?.Invoke(Id);
	
	/// <summary>
	/// Handles a click on the delete button.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked on the delete button on this virtual machine. <br/>
	/// Postcondition: A confirmation popup appears.
	/// </remarks>
	[RelayCommand]
	private void DeleteClick() => DeleteClicked?.Invoke(AsVmGeneralDescriptor());
}

public partial class DeletionDriveItemTemplate : ObservableObject
{
	public int Id { get; }
	public string Name { get; }
	public DriveType DriveType { get; }
	public int Size { get; }	/* The size of the drive, in MiB */

	public string DriveTypeString => DriveType.ToString();

	public string SizeString
	{
		get
		{
			if (Size >= 1024) return $"{(Size/1024.0):0.##} GiB";
			
			return $"{Size} MiB";
		}
	}
	
	[ObservableProperty]
	private bool _isInUse;
	
	[ObservableProperty] 
	private bool _isMarkedForDeletion;
	
	public DeletionDriveItemTemplate(int id, string name, DriveType driveType, int size, bool isDriveInUse)
	{
		Id = id;
		Name = name;
		DriveType = driveType;
		Size = size;
		IsInUse = isDriveInUse;
		IsMarkedForDeletion = !isDriveInUse;
	}
}

public partial class DriveConnectionItemTemplate : ObservableObject
{
	public static bool CanConnect { get; set; }
	public static bool CanDisconnect { get; set; }
	public int Id { get; }
	public string Name { get; }
	public string Size { get; }
	public DriveType DriveType { get; }
	public bool IsEnabled { get; }
	[ObservableProperty] 
	private bool _isChecked = false;

	public DriveConnectionItemTemplate(DriveGeneralDescriptor descriptor, bool connected)
	{
		Id = descriptor.Id;
		Name = descriptor.Name;
		DriveType = descriptor.DriveType;
		Size = descriptor.Size >= 1024 
			? $"{(descriptor.Size/1024.0):0.##} GiB" 
			: $"{descriptor.Size} MiB";
		
		IsChecked = connected;
		IsEnabled = connected ? CanDisconnect : CanConnect;
	}
}