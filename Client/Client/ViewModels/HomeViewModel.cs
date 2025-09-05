using System.Collections.ObjectModel;
using Client.Services;
using Shared;

namespace Client.ViewModels;

public class HomeViewModel : ViewModelBase
{
	public ObservableCollection<VmItemTemplate> VMs { get; }

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
		VMs = new ObservableCollection<VmItemTemplate>()
		{
			new  VmItemTemplate("My first VM", "Arch Linux"), 
			new  VmItemTemplate("My second VM", "Manjaro Linux"),
		};
	}
}

public class VmItemTemplate
{
	public string Name { get; set; }
	public string OperatingSystem { get; }
	public SharedDefinitions.VmState Status { get; set; }
	
	/// <summary>
	/// The status of the VM as a string.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: A string representation with space-seperated words of the Status property is returned.
	/// </remarks>
	public string StatusString => Common.SeparateStringWords(Status.ToString());

	/// <summary>
	/// Creates a new instance of a VmItemTemplate.
	/// </summary>
	/// <param name="name">
	/// The name of the VM. name != null.
	/// </param>
	/// <param name="operatingSystem">
	/// The operating system of the VM. operatingSystem != null.
	/// </param>
	/// <remarks>
	/// Precondition: name != null &amp;&amp; operatingSystem != null. <br/>
	/// Postcondition: A new instance of VmItemTemplate is created.
	/// </remarks>
	public VmItemTemplate(string name, string operatingSystem)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		Status = SharedDefinitions.VmState.ShutDown;
	}
}