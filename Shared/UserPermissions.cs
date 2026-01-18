using System.ComponentModel;
using System.Reflection;

namespace Shared;

[Flags]
public enum UserPermissions
{
	[Description("No permissions over the user.")]
	None = 0,

	[Description("Delete the user, along with all their virtual machines, drives, and sub-users in the same manner, recursively.")]
	UserDelete				= 1 << 0,
	
	[Description("List the user's virtual machines, along with their specifications. (name, OS, CPU, etc..)")]
	VirtualMachineList		= 1 << 1,
	
	[Description("Create virtual machines on the user's behalf. Includes the Virtual Machine List permission.")]
	VirtualMachineCreate	= 1 << 2,
	
	[Description("Delete the user's virtual machines. Includes the Virtual Machine List permission.")]
	VirtualMachineDelete	= 1 << 3,
	
	[Description("Watch the user's running virtual machines. Includes the Virtual Machine List permission.")]
	VirtualMachineWatch		= 1 << 4,
	
	[Description("Use the user's virtual machines. Includes the Virtual Machine Watch, and Virtual Machine List permissions.")]
	VirtualMachineUse		= 1 << 5,

	[Description("List the user's drives, along with their specifications. (name, size, type, etc..)")]
	DriveList				= 1 << 6,
	
	[Description("Create drives on the user's behalf. Includes the Drive List permission.")]
	DriveCreate				= 1 << 7,
	
	[Description("Delete the user's drives. Includes the Drive List permission.")]
	DriveDelete				= 1 << 8,

	[Description("List drive connections (which drive is connected to which VM) on the user's drives. Includes the Drive List and Virtual Machine List permissions.")]
	DriveConnectionList		= 1 << 9,
	
	[Description("Connect the user's drives to the user's virtual machines. Includes the Drive Connection List, Drive List, and Virtual Machine List permissions.")]
	DriveConnect			= 1 << 10,
	
	[Description("Disconnect the user's drives from the user's virtual machines. Includes the Drive Connection List, Drive List, and Virtual Machine List permissions.")]
	DriveDisconnect			= 1 << 11,
	
	[Description("List partitions, files, and directories on the user's drives. Includes the Drive List permission.")]
	DriveItemList			= 1 << 12,
	
	[Description("Create files and directories in the user's drives. Includes the Drive Item List, and Drive List permissions.")]
	DriveItemCreate			= 1 << 13,
	
	[Description("Delete files and directories from the user's drives. Includes the Drive Item List, and Drive List permissions.")]
	DriveItemDelete			= 1 << 14,
	
	[Description("Download files from the user's drives, or the drives themselves. Includes the Drive Item List, and Drive List permissions.")]
	DriveItemDownload		= 1 << 15,
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
		(permissions & permission) != 0 || permission == UserPermissions.None;		/* Everything has None, but None has nothing. */

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
				
				UserPermissions.DriveConnectionList				=> UserPermissions.DriveList | UserPermissions.VirtualMachineList,
				
				UserPermissions.DriveConnect or
					UserPermissions.DriveDisconnect				=> UserPermissions.DriveConnectionList | UserPermissions.DriveList | UserPermissions.VirtualMachineList,

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
	/// Checks whether the current permissions are valid - That all permissions, have their required permissions included.
	/// </summary>
	/// <param name="permissions">The current permissions.</param>
	/// <returns>True if the current permissions are valid, false otherwise.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns true if the current permissions are valid, false otherwise.
	/// </remarks>
	public static bool IsValid(this UserPermissions permissions)
	{
		UserPermissions[] prms = permissions.ToArray();
		foreach (UserPermissions permission in prms)
		{
			if (!permissions.HasPermission(permission.GetIncluded()))
				return false;
		}
		
		return true;
	}
	
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

	/// <summary>
	/// Get a description of this permission. Must be a single permission.
	/// </summary>
	/// <param name="permission">The current permission. Must be a single permission.</param>
	/// <returns>A description of the permission, or the permissions .ToString() if it does not have a description.</returns>
	/// <remarks>
	/// Precondition: The current given permission is a single permission. <br/>
	/// Postcondition: A description of the permission is returned, or the permissions .ToString() if it does not have a description.
	/// </remarks>
	public static string Description(this UserPermissions permission)
	{
		FieldInfo? fi = permission.GetType().GetField(permission.ToString());
		if (fi == null)
			return Common.SeparateStringWords(permission.ToString());
		
		DescriptionAttribute? attr = fi.GetCustomAttribute<DescriptionAttribute>();
		if (attr == null)
			return Common.SeparateStringWords(permission.ToString());
		
		return attr.Description;
	}
}