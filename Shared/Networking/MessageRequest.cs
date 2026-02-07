using Newtonsoft.Json;
using Shared.Drives;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Shared.Networking;

public interface IMessageRequest : IMessageTcp {}

public abstract class MessageRequest : Message, IMessageRequest
{
	protected MessageRequest() {}
	
	[JsonConstructor]
	protected MessageRequest(Guid id) : base(id) {}
}

public class MessageRequestCheckUsername : MessageRequest	/* Check if the provided username is available (that there is no such user) */
{
	public string Username { get; }

	public MessageRequestCheckUsername(string username)
	{
		Username = username;
	}

	[JsonConstructor]
	private MessageRequestCheckUsername(Guid id, string username)
		: base(id)
	{
		Username = username;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username);
}

public class MessageRequestCreateAccount : MessageRequest
{
	public string Username { get; }
	public string Email { get; }
	public string Password { get; }

	public MessageRequestCreateAccount(string username, string email, string password)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
	}
	
	[JsonConstructor]
	private MessageRequestCreateAccount(Guid id, string username, string email, string password)
		: base(id)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) 
	                                                               && !string.IsNullOrEmpty(Email) 
	                                                               && !string.IsNullOrEmpty(Password);
}

public class MessageRequestDeleteAccount : MessageRequest
{
	public int UserId { get; }

	public MessageRequestDeleteAccount(int userId)
	{
		UserId = userId;
	}
	
	[JsonConstructor]
	private MessageRequestDeleteAccount(Guid id, int userId)
		: base(id)
	{
		UserId = userId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

public class MessageRequestLogin : MessageRequest
{
	public string Username { get; }
	public string Password { get; }

	public MessageRequestLogin(string username, string password)
	{
		Username = username.Trim();
		Password = password;
	}

	[JsonConstructor]
	private MessageRequestLogin(Guid id, string username, string password)
		: base(id)
	{
		Username = username.Trim();
		Password = password;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) 
	                                                               && !string.IsNullOrEmpty(Password);
}

public class MessageRequestLogout : MessageRequest
{
	public MessageRequestLogout() {}
	
	[JsonConstructor]
	private MessageRequestLogout(Guid id)
		: base(id)
	{
	}
}

public class MessageRequestLoginSubUser : MessageRequest
{
	public int UserId { get; }

	public MessageRequestLoginSubUser(int userId)
	{
		UserId = userId;
	}
	
	[JsonConstructor]
	private MessageRequestLoginSubUser(Guid id, int userId)
		: base(id)
	{
		UserId = userId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

public class MessageRequestCreateSubUser : MessageRequest
{
	public string Username { get; }
	public string Email { get; }
	public string Password { get; }
	public UserPermissions Permissions { get; }

	public MessageRequestCreateSubUser(string username, string email, string password, UserPermissions permissions)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
		Permissions = permissions.AddIncluded();
	}
	
	[JsonConstructor]
	private MessageRequestCreateSubUser(Guid id, string username, string email, string password, UserPermissions permissions)
		: base(id)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
		Permissions = permissions.AddIncluded();
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) &&
	                                         !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password);
}

/* Set new permissions the owner will have over the given user. */
public class MessageRequestSetOwnerPermissions : MessageRequest
{
	public int UserId { get; }		/* The ID of the user to change the owner permissions of. */
	public UserPermissions Permissions { get; }

	public MessageRequestSetOwnerPermissions(int userId, UserPermissions permissions)
	{
		UserId = userId;
		Permissions = permissions;
	}
	
	[JsonConstructor]
	public MessageRequestSetOwnerPermissions(Guid id, int userId, UserPermissions permissions)
		: base(id)
	{
		UserId = userId;
		Permissions = permissions;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

public class MessageRequestResetPassword : MessageRequest
{
	public string Password { get; }
	public string NewPassword { get; }

	public MessageRequestResetPassword(string password, string newPassword)
	{
		Password = password;
		NewPassword = newPassword;
	}

	[JsonConstructor]
	private MessageRequestResetPassword(Guid id, string password, string newPassword)
		: base(id)
	{
		Password = password;
		NewPassword = newPassword;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(NewPassword);
}

public class MessageRequestListSubUsers : MessageRequest
{
	public MessageRequestListSubUsers() {}
	
	[JsonConstructor]
	private MessageRequestListSubUsers(Guid id) : base(id) {}
}

public class MessageRequestCreateVm : MessageRequest
{
	public string Name { get; }
	public OperatingSystem OperatingSystem { get; }
	public CpuArchitecture CpuArchitecture { get; }
	public int RamSizeMiB { get; }
	public BootMode BootMode { get; }

	public MessageRequestCreateVm(string name, OperatingSystem operatingSystem, 
		CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		RamSizeMiB = ramSizeMiB;
		BootMode = bootMode;
	}

	[JsonConstructor]
	private MessageRequestCreateVm(Guid id, string name, OperatingSystem operatingSystem, 
		CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
		: base(id)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		RamSizeMiB = ramSizeMiB;
		BootMode = bootMode;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name) 
	                                                               && RamSizeMiB > 0 && RamSizeMiB <= SharedDefinitions.VmRamSizeMbMax 
	                                                               && Enum.IsDefined(typeof(OperatingSystem), OperatingSystem) 
	                                                               && Enum.IsDefined(typeof(CpuArchitecture), CpuArchitecture) 
	                                                               && Enum.IsDefined(typeof(BootMode), BootMode);
}

public class MessageRequestDeleteVm : MessageRequest
{
	public int VmId { get; }

	public MessageRequestDeleteVm(int vmId)
	{
		VmId = vmId;
	}

	[JsonConstructor]
	private MessageRequestDeleteVm(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestListVms : MessageRequest
{
	public MessageRequestListVms() {}
	
	[JsonConstructor]
	private MessageRequestListVms(Guid id) : base(id) { }
}

public class MessageRequestCheckVmExist : MessageRequest	/* Check if there is a virtual machine with the given name */
{
	public string Name { get; }

	public MessageRequestCheckVmExist(string name)
	{
		Name = name;
	}
	
	[JsonConstructor]
	private MessageRequestCheckVmExist(Guid id, string name)
		: base(id)
	{
		Name = name;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name);
}

public class MessageRequestCreateDriveFs : MessageRequest
{
	public string Name { get; }
	public int SizeMb { get; }
	public FileSystemType FileSystem { get; }

	public MessageRequestCreateDriveFs(string name, int sizeMb, FileSystemType fileSystem)
	{
		Name = name;
		SizeMb = sizeMb;
		FileSystem = fileSystem;
	}
	
	[JsonConstructor]
	private MessageRequestCreateDriveFs(Guid id, string name, int sizeMb, FileSystemType fileSystem)
		: base(id)
	{
		Name = name;
		SizeMb = sizeMb;
		FileSystem = fileSystem;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name) 
	                                                               && SizeMb > 0 
	                                                               && SizeMb <= SharedDefinitions.DriveSizeMbMax;
}

public class MessageRequestCreateDriveFromImage : MessageRequest
{
	public string Name { get; }
	public DriveType Type { get; }
	public ulong Size { get; }

	public MessageRequestCreateDriveFromImage(string name, DriveType type, ulong size)
	{
		Name = name;
		Type = type;
		Size = size;
	}
	
	[JsonConstructor]
	private MessageRequestCreateDriveFromImage(Guid id, string name, DriveType type, ulong size)
		: base(id)
	{
		Name = name;
		Type = type;
		Size = size;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrWhiteSpace(Name) && Enum.IsDefined(typeof(DriveType), Type)
	                                                               && Size / 1024UL / 1024UL <= SharedDefinitions.DriveSizeMbMax;
}

public class MessageRequestCreateDriveOs : MessageRequest
{
	public string Name { get; }
	public int SizeMiB { get; }
	
	public OperatingSystem OperatingSystem { get; }

	public MessageRequestCreateDriveOs(string name, int sizeMiB, OperatingSystem operatingSystem)
	{
		Name = name;
		SizeMiB = sizeMiB;
		OperatingSystem = operatingSystem;
	}
	
	[JsonConstructor]
	private MessageRequestCreateDriveOs(Guid id, string name, int sizeMiB, OperatingSystem operatingSystem)
		: base(id)
	{
		Name = name;
		SizeMiB = sizeMiB;
		OperatingSystem = operatingSystem;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name) 
	                                                               && Enum.IsDefined(typeof(OperatingSystem), OperatingSystem)
	                                                               && OperatingSystem != OperatingSystem.Other
	                                                               && Common.IsOperatingSystemDriveSizeValid(OperatingSystem, SizeMiB);
}

public class MessageRequestConnectDrive : MessageRequest	/* Request to mark the drive as connected to some VM. */
{
	public int DriveId { get; }
	public int VmId { get; }
	
	public MessageRequestConnectDrive(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestConnectDrive(Guid id, int driveId, int vmId)
		: base(id)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

public class MessageRequestDisconnectDrive : MessageRequest
{
	public int DriveId { get; }
	public int VmId { get; }

	public MessageRequestDisconnectDrive(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestDisconnectDrive(Guid id, int driveId, int vmId)
		: base(id)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

public class MessageRequestListDriveConnections : MessageRequest
{
	public MessageRequestListDriveConnections() { }
	
	[JsonConstructor]
	private MessageRequestListDriveConnections(Guid id) : base(id) { }
}

public class MessageRequestListDrives : MessageRequest
{
	public MessageRequestListDrives() {}
	
	[JsonConstructor]
	private MessageRequestListDrives(Guid id) : base(id) {}
}

public class MessageRequestListPathItems : MessageRequest
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestListPathItems(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	[JsonConstructor]
	private MessageRequestListPathItems(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestDownloadItem : MessageRequest	/* Download from client perspective - client receives the item from the server. */
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestDownloadItem(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	[JsonConstructor]
	private MessageRequestDownloadItem(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestUploadFile : MessageRequest		/* Upload from client perspective - client sends the file to the server. */
{
	public int DriveId { get; }
	public string Path { get; }		/* Destination inside the drive. Includes the filename. */
	public ulong Size { get; }		/* In bytes. */

	public MessageRequestUploadFile(int driveId, string path, ulong size)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
		Size = size;
	}
	
	[JsonConstructor]
	private MessageRequestUploadFile(Guid id, int driveId, string path, ulong size)
		: base(id)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
		Size = size;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && !string.IsNullOrEmpty(Path) &&
	                                         Size > 0 && Size <= SharedDefinitions.DriveSizeMbMax * 1024UL * 1024UL;
}

public class MessageRequestCreateDirectory : MessageRequest
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestCreateDirectory(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	[JsonConstructor]
	private MessageRequestCreateDirectory(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && !string.IsNullOrEmpty(Path);
}

public class MessageRequestDeleteItem : MessageRequest
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestDeleteItem(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	[JsonConstructor]
	private MessageRequestDeleteItem(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestVmStartup : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmStartup(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestVmStartup(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmShutdown : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmShutdown(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestVmShutdown(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmForceOff : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmForceOff(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestVmForceOff(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmStreamStart : MessageRequest		/* Request to send a video stream of the screen of a virtual machine (through MessageInfo) */
{
	public int VmId { get; }

	public MessageRequestVmStreamStart(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestVmStreamStart(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmStreamStop : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmStreamStop(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageRequestVmStreamStop(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}