using System;
using System.Collections.ObjectModel;
using Server.Services;
using Shared;

namespace Server.ViewModels;

public class UsersViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;
	
	public ObservableCollection<UserItemTemplate> Users { get; }

	public UsersViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
	
		/* Temporary */
		Users = new ObservableCollection<UserItemTemplate>()
		{
			new UserItemTemplate(1, "d", "d@gmail.com", DateTime.Now),
			new UserItemTemplate(2, "child", "child@gmail.com", DateTime.Now, 1, "d", UserPermissions.VirtualMachineCreate),
		};
	}

	/* Use for IDE preview only. */
	public UsersViewModel()
	{
		_databaseService = null!;
		Users = new ObservableCollection<UserItemTemplate>()
		{
			new UserItemTemplate(1, "d", "d@gmail.com", DateTime.Now),
			new UserItemTemplate(2, "child", "child@gmail.com", DateTime.Now, 1, "d", UserPermissions.VirtualMachineCreate),
		};
	}
}

public class UserItemTemplate
{
	public int Id { get; }
	public string Username { get; }
	public string Email { get; }
	public DateTime CreatedAt { get; }
	public bool IsSubUser { get; }
	public int? OwnerId { get; }
	public string? OwnerUsername { get; }
	public UserPermissionItemTemplate[]? Permissions { get; }
	
	public UserItemTemplate(int id, string username, string email, DateTime createdAt, int? ownerId, string? ownerUsername, UserPermissions ownerPermissions)
	{
		Id = id;
		Username = username;
		Email = email;
		CreatedAt = createdAt;
		IsSubUser = ownerId.HasValue;
		OwnerId = ownerId;
		OwnerUsername = ownerUsername;
		
		UserPermissions[] prms = ownerPermissions.AddIncluded().ToArray();
		Permissions = new UserPermissionItemTemplate[Math.Max(prms.Length, 1)];
		for (int i = 0; i < prms.Length; ++i)
			Permissions[i] = new UserPermissionItemTemplate(prms[i]);

		if (prms.Length == 0)
			Permissions[0] = new UserPermissionItemTemplate(UserPermissions.None);
	}
	
	public UserItemTemplate(int id, string username, string email, DateTime createdAt)
	{
		Id = id;
		Username = username;
		Email = email;
		CreatedAt = createdAt;
		IsSubUser = false;
		OwnerId = null;
		OwnerUsername = null;
		Permissions = null;
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