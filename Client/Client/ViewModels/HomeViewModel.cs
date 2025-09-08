using System.Collections.ObjectModel;
using Client.Services;
using Shared;

namespace Client.ViewModels;

public class HomeViewModel : ViewModelBase
{
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

	private void OnVmListChanged(object? sender, SharedDefinitions.VmGeneralDescriptor[] vms)
	{
		Vms.Clear();
		foreach (SharedDefinitions.VmGeneralDescriptor vm in vms)
		{
			Vms.Add(new VmItemTemplate(vm.Name, vm.OperatingSystem, vm.State));
		}
	}
}

public class VmItemTemplate
{
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
	public VmItemTemplate(string name, SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.VmState state)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		State = state;
	}
}