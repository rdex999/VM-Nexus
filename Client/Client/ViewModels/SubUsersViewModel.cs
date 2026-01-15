using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
		_ = InitializeAsync();
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

	/// <summary>
	/// Initializes this view model. Fetches sub-users and displays them.
	/// </summary>
	/// <remarks>
	/// Precondition: User is logged in. <br/>
	/// Postcondition: On success, the users are fetched and displayed. On failure, the users are not displayed. (Failure should not happen)
	/// </remarks>
	private async Task InitializeAsync()
	{
		User[]? subUsers = await ClientSvc.GetSubUsersAsync();
		if (subUsers == null)
			return;

		SubUsers.Clear();
		foreach (var subUser in subUsers)
			SubUsers.Add(new SubUserItemTemplate(subUser.Username, subUser.OwnerPermissions, DateOnly.FromDateTime(subUser.CreatedAt)));
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
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
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