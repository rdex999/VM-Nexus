using Newtonsoft.Json;

namespace Shared;

public class User
{
	public int Id { get; }
	public int? OwnerId { get; }
	public UserPermissions OwnerPermissions { get; }
	public string Username { get; }
	public string Email { get; }
	public DateTime CreatedAt { get; }
	public bool IsSubUser => OwnerId != null;

	[JsonConstructor]
	public User(int id, int? ownerId, UserPermissions ownerPermissions, string username, string email, DateTime createdAt)
	{
		Id = id;
		OwnerId = ownerId;
		OwnerPermissions = ownerPermissions;
		Username = username.Trim();
		Email = email.Trim();
		CreatedAt = createdAt;
	}

	public User(int id, string username, string email, DateTime createdAt)
	{
		Id = id;
		OwnerId = null;
		OwnerPermissions = 0;
		Username = username.Trim();
		Email = email.Trim();
		CreatedAt = createdAt;
	}
}