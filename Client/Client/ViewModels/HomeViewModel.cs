using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Interactivity;
using Client.Services;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace Client.ViewModels;

public class HomeViewModel : ViewModelBase
{
	public event EventHandler<SharedDefinitions.VmGeneralDescriptor>? VmOpenClicked;
	public ObservableCollection<VmItemTemplate> Vms { get; }

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
		foreach (SharedDefinitions.VmGeneralDescriptor vm in vms)
		{
			Vms.Add(new VmItemTemplate(vm.Id, vm.Name, vm.OperatingSystem, vm.State));
			Vms.Last().OpenClicked += OnVmOpenClicked;
		}
	}

	/// <summary>
	/// Handles a click on the Open button of one of the users VMs. Open a new tab for the VM. If a tab exists, redirect the user to it.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the Open button on a VM. <br/>
	/// Postcondition: A new tab is opened for the VM. If a tab for the VM is already open, the user will be redirected to it.
	/// </remarks>
	private void OnVmOpenClicked(object? sender, SharedDefinitions.VmGeneralDescriptor descriptor)
	{
		VmOpenClicked?.Invoke(this, descriptor);
	}
}

public partial class VmItemTemplate
{
	public event EventHandler<SharedDefinitions.VmGeneralDescriptor>? OpenClicked;
	public int Id { get; }
	public string Name { get; }
	public SharedDefinitions.OperatingSystem OperatingSystem { get; }

	public string OperatingSystemString
	{
		get
		{
			if (OperatingSystem == SharedDefinitions.OperatingSystem.Other)
			{
				return "Unknown OS";
			}
			return Common.SeparateStringWords(OperatingSystem.ToString());
		}
	}
	public SharedDefinitions.VmState State { get; }
	public string StateString => Common.SeparateStringWords(State.ToString());

	/// <summary>
	/// Creates a new instance of a VmItemTemplate.
	/// </summary>
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
	/// Precondition: name != null. <br/>
	/// Postcondition: A new instance of VmItemTemplate is created.
	/// </remarks>
	public VmItemTemplate(int id, string name, SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.VmState state)
	{
		Id = id;
		Name = name;
		OperatingSystem = operatingSystem;
		State = state;
	}

	/// <summary>
	/// Handles opening the VM - opens a tab for the VM.
	/// </summary>
	/// <remarks>
	/// Precondition: User has clicked the Open button on a VM. <br/>
	/// Postcondition: A new tab is opened for the VM. If a tab for the VM is already open, the user will be redirected to it.
	/// </remarks>
	[RelayCommand]
	private void OpenClick()
	{
		OpenClicked?.Invoke(this, new SharedDefinitions.VmGeneralDescriptor(Id, Name, OperatingSystem, State));
	}
}