namespace Shared;

[Flags]
public enum UserPermissions
{
	VirtualMachineList		= 1 << 0,
	VirtualMachineCreate	= 1 << 1,
	VirtualMachineDelete	= 1 << 2,
	VirtualMachineWatch		= 1 << 3,
	VirtualMachineUse		= 1 << 4,
	
	DriveList				= 1 << 5,
	DriveCreate				= 1 << 6,
	DriveDelete				= 1 << 7,
	DriveItemList			= 1 << 8,
	DriveItemCreate			= 1 << 9,
	DriveItemDelete			= 1 << 10,
	DriveItemDownload		= 1 << 11,
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

	/// <summary>
	/// Get the permissions that the given permissions need in order to be valid. <br/>
	/// For example, the VM use permission depends on the VM watch permission, which depends on the VM list permission.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <returns>The permissions that the given permissions depend on.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: The permissions that the given permissions depend on are returned.
	/// </remarks>
	public static UserPermissions GetIncluded(this UserPermissions permissions)
	{
		UserPermissions result = 0;
		UserPermissions[] prms = permissions.ToArray();

		foreach (UserPermissions permission in prms)
		{
			result |= permission switch
			{
				UserPermissions.VirtualMachineCreate or
					UserPermissions.VirtualMachineDelete or
					UserPermissions.VirtualMachineWatch			=> UserPermissions.VirtualMachineList,

				UserPermissions.VirtualMachineUse				=> UserPermissions.VirtualMachineList | UserPermissions.VirtualMachineWatch,

				UserPermissions.DriveCreate or
					UserPermissions.DriveDelete or
					UserPermissions.DriveItemList				=> UserPermissions.DriveList,

				UserPermissions.DriveItemCreate or
					UserPermissions.DriveItemDelete or
					UserPermissions.DriveItemDownload			=> UserPermissions.DriveItemList | UserPermissions.DriveList,

				_ => 0
			};
		}

		return result;
	}

	/// <summary>
	/// Add the required permissions to the current ones, for them to be valid. (Add dependant permissions)
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <returns>The original permissions, along with the permissions they depend on.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: The original permissions along with the permissions they depend on are returned.
	/// </remarks>
	public static UserPermissions AddIncluded(this UserPermissions permissions) => permissions | permissions.GetIncluded();
	
	/// <summary>
	/// Converts into a permission array, while each element is a single permission.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <returns>A permission array, where each element is a single permission.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: A permission array is returned, in which each element is a single permission from the original given permissions.
	/// </remarks>
	public static UserPermissions[] ToArray(this UserPermissions permissions)
	{
		int p = (int)permissions;
		UserPermissions[] prms = new UserPermissions[int.PopCount(p)];
		int skipped = 0;
		for (int i = 0; i < prms.Length; ++i)
		{
			int position = int.TrailingZeroCount(p);
			prms[i] = (UserPermissions)(1 << (position + skipped));

			p >>= position + 1;
			skipped += position + 1;
		}
		
		return prms;
	}
}