using MessagePack;

namespace Shared;

[Union(0, typeof(User))]
[Union(1, typeof(SubUser))]
public interface IUser
{
	int Id { get; }
	string Username { get; }
	string Email { get; }
	DateTime CreatedAt { get; }
}

[MessagePackObject]
public class User : IUser
{
	[Key(0)]
	public int Id { get; set; }

	[Key(1)] 
	public string Username { get; set; } = null!;

	[Key(2)] 
	public string Email { get; set; } = null!;
	
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
	public string OwnerUsername { get; set; } = null!;

	[Key(7)] 
	public string OwnerEmail { get; set; } = null!;
	
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