using System.Collections.ObjectModel;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;

namespace Client.ViewModels;

public class SubUsersViewModel : ViewModelBase
{
	public ObservableCollection<SubUserItemTemplate> SubUsers { get; set; }
	
	public SubUsersViewModel(NavigationService navigationSvc, ClientService clientSvc) 
		: base(navigationSvc, clientSvc)
	{
	}

	public SubUsersViewModel()
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>()
		{
			new SubUserItemTemplate("hey0", UserPermissions.DriveList),
			new SubUserItemTemplate("hey1", UserPermissions.DriveItemDownload),
			new SubUserItemTemplate("user2", UserPermissions.VirtualMachineUse),
		};
	}
}

public class SubUserItemTemplate : ObservableObject
{
	public string UserName { get; }
	public UserPermissions Permissions { get; }		/* Owner's permissions over this sub-user. */
	
	public SubUserItemTemplate(string userName, UserPermissions permissions)
	{
		UserName = userName;
		Permissions = permissions;
	}
}