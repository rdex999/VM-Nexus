using System.Collections.ObjectModel;
using Client.Services;

namespace Client.ViewModels;

public class HomeViewModel : ViewModelBase
{
	public ObservableCollection<VmItemTemplate> VMs { get; }
	
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
	public VmStatus Status { get; set; }
	public string StatusString 
	{
		get
		{
			/* Seperate the words with spaces */
			string result = Status.ToString();
			for (int i = 0; i < result.Length; i++)
			{
				if (result[i] >= 'A' && result[i] <= 'Z' && i != 0)
				{
					result = result.Insert(i, " ");
					++i;
				}
			}
			return result;
		}
	}

	public VmItemTemplate(string name, string operatingSystem)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		Status = VmStatus.ShutDown;
	}
	
	public enum VmStatus
	{
		ShutDown,
		Running,
		Sleeping,
		Hibernated,
	}
}