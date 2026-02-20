using MessagePack;

namespace Shared;

[MessagePackObject]
public class User
{
	[Key(0)]
	public int Id { get; set; }
	
	[Key(1)]
	public string Username { get; set; }
	
	[Key(2)]
	public string Email { get; set; }
	
	[Key(3)]
	public DateTime CreatedAt { get; set; }

	public User() {}
	
	public User(int id, string username, string email, DateTime createdAt)
	{
		Id = id;
		Username = username.Trim();
		Email = email.Trim();
		CreatedAt = createdAt;
	}
}

[MessagePackObject]
public class SubUser : User
{
	[Key(4)]
	public int OwnerId { get; set; }
	[Key(5)]
	public UserPermissions OwnerPermissions { get; set; }
	[Key(6)]
	public string OwnerUsername { get; set; }
	[Key(7)]
	public string OwnerEmail { get; set; }
	
	public SubUser() {}
	
	public SubUser(int id, int ownerId, UserPermissions ownerPermissions, string ownerUsername, string ownerEmail, string username, string email, DateTime createdAt)
		: base(id, username, email, createdAt)
	{
		OwnerId = ownerId;
		OwnerPermissions = ownerPermissions;
		OwnerUsername = ownerUsername;
		OwnerEmail = ownerEmail;
	}
}