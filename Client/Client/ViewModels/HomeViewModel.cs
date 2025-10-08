using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;
using Shared.Networking;

namespace Client.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
	public event EventHandler<SharedDefinitions.VmGeneralDescriptor>? VmOpenClicked;
	public ObservableCollection<VmItemTemplate> Vms { get; }

	private VmItemTemplate _forceOffWarningVm;
	
	[ObservableProperty] 
	private bool _forceOffWarningIsOpen = false;
	
	[ObservableProperty]
	private string _forceOffWarningQuestion = string.Empty;

	[ObservableProperty]
	private bool _deleteVmPopupIsOpen = false;

	[ObservableProperty] 
	private bool _deleteVmPopupHasDrives = false;
	
	public ObservableCollection<DeletionDriveItemTemplate> DeleteVmPopupDrives { get; set; }		/* Drives the user can select to delete, when deleting a VM */
	
	/// <summary>
	/// Initializes a new instance of HomeViewModel.
	/// </summary>
	/// <param name="navigationSvc">
	/// The navigation service. navigationSvc != null.
	/// </param>
	/// <param name="clientSvc">
	/// The client service. clientSvc != null.
	/// </param>
	/// <remarks>
	/// Precondition: MainView is created, (HomeViewModel is the default side menu selection) or the user selects the home page in the side menu. <br/>
	/// Postcondition: A new instance of HomeViewModel is created.
	/// </remarks>
	public HomeViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		Vms = new ObservableCollection<VmItemTemplate>();
		DeleteVmPopupDrives = new ObservableCollection<DeletionDriveItemTemplate>();

		ClientSvc.VmListChanged += OnVmListChanged;
	}

	/// <summary>
	/// Handles a change in the VMs. A change is, for example, that a new VM is available, or the state of one or more VMs has changed.
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="vms">An updated list of the users virtual machines. vms != null.</param>
	/// <remarks>
	/// Precondition: There was a change in the information of one or more of the users VMs, or some VMs were created/deleted. vms != null. <br/>
	/// Postcondition: The change is handled, the virtual machines list is updated along with the UI.
	/// </remarks>
	private void OnVmListChanged(object? sender, SharedDefinitions.VmGeneralDescriptor[] vms)
	{
		Vms.Clear();
		Dispatcher.UIThread.InvokeAsync(() => { }).Wait();			/* Allow the UI thread to process any changes that were made. */
		foreach (SharedDefinitions.VmGeneralDescriptor vm in vms)
		{
			Dispatcher.UIThread.Invoke(() =>
			{
				VmItemTemplate template = new VmItemTemplate(ClientSvc, vm.Id, vm.Name, vm.OperatingSystem, vm.State);
				template.OpenClicked += OnVmOpenClicked;
				template.ForceOffClicked += OnVmForceOffClicked;
				template.DeleteClicked += OnVmDeleteClicked;
				Vms.Add(template);
			});
		}
	}

	/// <summary>
	/// Handles a click on the Open button of one of the users VMs. Open a new tab for the VM. If a tab exists, redirect the user to it.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the Open button on a VM. <br/>
	/// Postcondition: A new tab is opened for the VM. If a tab for the VM is already open, the user will be redirected to it.
	/// </remarks>
	private void OnVmOpenClicked(SharedDefinitions.VmGeneralDescriptor descriptor) =>
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
	private void OnVmDeleteClicked(SharedDefinitions.VmGeneralDescriptor vm)
	{
		Task<MessageResponseListConnectedDrives?> task = ClientSvc.GetConnectedDrivesAsync(vm.Id);
		task.GetAwaiter().OnCompleted(() =>
		{
			if (task.Result == null || task.Result.Result != MessageResponseListConnectedDrives.Status.Success)
			{
				DeletePopupClosed();
				return;
			}

			SharedDefinitions.DriveGeneralDescriptor[] drives = task.Result.Drives!;
		
			DeleteVmPopupHasDrives = drives.Length != 0;
		
			DeleteVmPopupDrives.Clear();
			foreach (SharedDefinitions.DriveGeneralDescriptor drive in drives)
			{
				DeleteVmPopupDrives.Add(new DeletionDriveItemTemplate(drive.Id, drive.Name, drive.DriveType, drive.Size));
			}
		
			DeleteVmPopupIsOpen = true;
		});
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

	[RelayCommand]
	private async Task DeleteVmConfirmDeleteClickAsync()
	{
		
	}
}

public partial class VmItemTemplate : ObservableObject
{
	public Action<SharedDefinitions.VmGeneralDescriptor>? OpenClicked;
	public Action<VmItemTemplate>? ForceOffClicked;
	public Action<SharedDefinitions.VmGeneralDescriptor>? DeleteClicked;
	
	public int Id { get; }

	[ObservableProperty] 
	private string _name;

	private SharedDefinitions.OperatingSystem _operatingSystem;
	public SharedDefinitions.OperatingSystem OperatingSystem
	{
		get => _operatingSystem;
		private set
		{
			_operatingSystem = value;
			OperatingSystemString = _operatingSystem == SharedDefinitions.OperatingSystem.Other
				? "Unknown OS"
				: Common.SeparateStringWords(_operatingSystem.ToString());
		}
	}
	
	private readonly ClientService _clientService;

	[ObservableProperty]
	private string _operatingSystemString = string.Empty;

	private SharedDefinitions.VmState _state;
	public SharedDefinitions.VmState State
	{
		get => _state;
		set
		{
			_state = value;
			StateString = Common.SeparateStringWords(_state.ToString());
			if (_state == SharedDefinitions.VmState.ShutDown)
			{
				StateColor = new SolidColorBrush(Color.FromRgb(0x4F, 0x5B, 0x5B));
			}
			else if (_state == SharedDefinitions.VmState.Running)
			{
				StateColor = new SolidColorBrush(Color.FromRgb(0x6B, 0xE5, 0x78));
			}
		}
	}

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
	/// <param name="id">The ID of the virtual machine. id >= 1.</param>
	/// <param name="name">
	/// The name of the VM. name != null.
	/// </param>
	/// <param name="operatingSystem">
	/// The operating system of the VM. operatingSystem != null.
	/// </param>
	/// <param name="state">
	/// The state of the VM. (shut down, running, etc..)
	/// </param>
	/// <remarks>
	/// Precondition: clientService != null &amp;&amp; id >= 1 &amp;&amp; name != null. <br/>
	/// Postcondition: A new instance of VmItemTemplate is created.
	/// </remarks>
	public VmItemTemplate(ClientService clientService, int id, string name,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.VmState state)
	{
		_clientService = clientService;
		Id = id;
		Name = name;
		OperatingSystem = operatingSystem;
		State = state;
		_clientService.VmPoweredOn += OnVmPoweredOn;
		_clientService.VmPoweredOff += OnVmPoweredOff;
		_clientService.VmCrashed += OnVmCrashed;
	}

	/// <summary>
	/// Returns a VM general descriptor of this VM item template.
	/// </summary>
	/// <returns>A VM general descriptor of this VM item template.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: A VM general descriptor of this VM item template is returned.
	/// </remarks>
	public SharedDefinitions.VmGeneralDescriptor AsVmGeneralDescriptor() => 
		new SharedDefinitions.VmGeneralDescriptor(Id, Name, OperatingSystem, State);
	
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
			State = SharedDefinitions.VmState.Running;
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
			State = SharedDefinitions.VmState.ShutDown;
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
			State = SharedDefinitions.VmState.ShutDown;
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
		{
			State = SharedDefinitions.VmState.Running;
		} 
		else if(result != MessageResponseVmStartup.Status.Success)
		{
			ErrorMessage = "VM startup failed.";
		}
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
			State = SharedDefinitions.VmState.ShutDown;
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
	public SharedDefinitions.DriveType DriveType { get; }
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
	private bool _isChecked = false;

	public DeletionDriveItemTemplate(int id, string name, SharedDefinitions.DriveType driveType, int size)
	{
		Id = id;
		Name = name;
		DriveType = driveType;
		Size = size;
	}
}