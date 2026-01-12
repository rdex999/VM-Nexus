namespace Shared;

[Flags]
public enum UserPermissions
{
	VirtualMachineList		= 1 << 0,
	VirtualMachineCreate	= VirtualMachineList | (1 << 1),
	VirtualMachineDelete	= VirtualMachineList | (1 << 2),
	VirtualMachineWatch		= VirtualMachineList | (1 << 3),
	VirtualMachineUse		= VirtualMachineWatch | (1 << 4),
	
	DriveList				= 1 << 5,
	DriveCreate				= DriveList | (1 << 6),
	DriveDelete				= DriveList | (1 << 7),
	DriveItemList			= DriveList | (1 << 8),
	DriveItemCreate			= DriveItemList | (1 << 9),
	DriveItemDelete			= DriveItemList | (1 << 10),
	DriveItemDownload		= DriveItemList | (1 << 11),
}

public static class UserPermissionsExtensions
{
	/// <summary>
	/// Check if permissions include the given permission.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <param name="permission">The permission to check if included in the current permissions.</param>
	/// <returns>True if the given permission exists in the current permissions, false otherwise.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns true if the given permission exists in the current permissions, false otherwise.
	/// </remarks>
	public static bool HasPermission(this UserPermissions permissions, UserPermissions permission) =>
		(permissions & permission) != 0;

	/// <summary>
	/// Adds the given permission to the current permissions.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <param name="permission">The permission to add to the current ones.</param>
	/// <returns>The new permissions, the old ones including the given new permission.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns the new permissions, the old ones including the given new permission.
	/// </remarks>
	public static UserPermissions AddPermission(this UserPermissions permissions, UserPermissions permission) =>
		permissions | permission;
	
	/// <summary>
	/// Removes the given permission from the current permissions.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <param name="permission">The permission to remove from the current ones.</param>
	/// <returns>The new permissions, the old ones without the given permission.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns the new permissions, the old ones without the given permission.
	/// </remarks>
	public static UserPermissions RemovePermission(this UserPermissions permissions, UserPermissions permission) =>
		permissions & ~permission;
}