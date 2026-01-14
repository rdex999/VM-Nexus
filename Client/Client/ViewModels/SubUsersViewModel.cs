using System;
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
		SubUsers = new ObservableCollection<SubUserItemTemplate>();
	}

	public SubUsersViewModel()
	{
		SubUsers = new ObservableCollection<SubUserItemTemplate>()
		{
			new SubUserItemTemplate("hey0", UserPermissions.DriveList, new DateOnly(1999, 12, 31)),
			new SubUserItemTemplate("hey1", UserPermissions.DriveItemDownload,  new DateOnly(1999, 12, 31)),
			new SubUserItemTemplate("user2", UserPermissions.VirtualMachineUse,  new DateOnly(1999, 5, 4)),
		};
	}
}

public class SubUserItemTemplate : ObservableObject
{
	public string UserName { get; }
	public UserPermissionItemTemplate[] Permissions { get; }		/* Owner's permissions over this sub-user. */
	public string Created { get; }
	
	public SubUserItemTemplate(string userName, UserPermissions permissions, DateOnly created)
	{
		UserName = userName;
		Created = created.ToString("dd/MM/yyyy");

		UserPermissions[] prms = permissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[prms.Length];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);
	}
}

public class UserPermissionItemTemplate
{
	public string Permission { get; }
	public string Description { get; }

	public UserPermissionItemTemplate(UserPermissions permission)
	{
		Permission = Common.SeparateStringWords(permission.ToString());
		Description = permission.Description();
	}
}