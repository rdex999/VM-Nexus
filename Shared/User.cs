using Newtonsoft.Json;

namespace Shared;

public class User
{
	public int Id { get; }
	public string Username { get; }
	public string Email { get; }
	public DateTime CreatedAt { get; }

	[JsonConstructor]
	public User(int id, string username, string email, DateTime createdAt)
	{
		Id = id;
		Username = username.Trim();
		Email = email.Trim();
		CreatedAt = createdAt;
	}
}

public class SubUser : User
{
	public int OwnerId { get; }
	public UserPermissions OwnerPermissions { get; }
	public string OwnerUsername { get; }
	public string OwnerEmail { get; }

	[JsonConstructor]
	public SubUser(int id, int ownerId, UserPermissions ownerPermissions, string ownerUsername, string ownerEmail, string username, string email, DateTime createdAt)
		: base(id, username, email, createdAt)
	{
		OwnerId = ownerId;
		OwnerPermissions = ownerPermissions;
		OwnerUsername = ownerUsername;
		OwnerEmail = ownerEmail;
	}
}