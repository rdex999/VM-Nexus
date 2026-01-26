using System;
using System.Collections.ObjectModel;
using Server.Services;

namespace Server.ViewModels;

public class UsersViewModel : ViewModelBase
{
	private readonly DatabaseService _databaseService;
	
	public ObservableCollection<UserItemTemplate> Users { get; }

	public UsersViewModel(DatabaseService databaseService)
	{
		_databaseService = databaseService;
		Users = new ObservableCollection<UserItemTemplate>();
	}

	/* Use for IDE preview only. */
	public UsersViewModel()
	{
		_databaseService = null!;
		Users = new ObservableCollection<UserItemTemplate>()
		{
			new UserItemTemplate(1, "d", "d@gmail.com", DateTime.Now, null, null),
			new UserItemTemplate(2, "child", "child@gmail.com", DateTime.Now, 1, "d"),
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

	public UserItemTemplate(int id, string username, string email, DateTime createdAt, int? ownerId, string? ownerUsername)
	{
		Id = id;
		Username = username;
		Email = email;
		CreatedAt = createdAt;
		IsSubUser = ownerId.HasValue;
		OwnerId = ownerId;
		OwnerUsername = ownerUsername;
	}
}