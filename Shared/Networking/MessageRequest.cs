using MessagePack;
using Shared.Drives;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Shared.Networking;

public interface IMessageRequest : IMessageTcp {}

public abstract class MessageRequest : Message, IMessageRequest
{
	protected MessageRequest() {}
}

[MessagePackObject]
public class MessageRequestCheckUsername : MessageRequest
{
	[Key(1)]
	public string Username { get; set; }

	public MessageRequestCheckUsername() { }

	public MessageRequestCheckUsername(string username)
	{
		Username = username;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username);
}

[MessagePackObject]
public class MessageRequestCreateAccount : MessageRequest
{
	[Key(1)]
	public string Username { get; set; }
	
	[Key(2)]
	public string Email { get; set; }
	
	[Key(3)]
	public string Password { get; set; }

	public MessageRequestCreateAccount() { }

	public MessageRequestCreateAccount(string username, string email, string password)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) 
	                                                               && !string.IsNullOrEmpty(Email) 
	                                                               && !string.IsNullOrEmpty(Password);
}

[MessagePackObject]
public class MessageRequestDeleteAccount : MessageRequest
{
	[Key(1)]
	public int UserId { get; set; }

	public MessageRequestDeleteAccount() { }

	public MessageRequestDeleteAccount(int userId)
	{
		UserId = userId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

[MessagePackObject]
public class MessageRequestLogin : MessageRequest
{
	[Key(1)]
	public string Username { get; set; }
	
	[Key(2)]
	public string Password { get; set; }

	public MessageRequestLogin() { }

	public MessageRequestLogin(string username, string password)
	{
		Username = username.Trim();
		Password = password;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) 
	                                                               && !string.IsNullOrEmpty(Password);
}

[MessagePackObject]
public class MessageRequestLogout : MessageRequest
{
	public MessageRequestLogout() {}
}

[MessagePackObject]
public class MessageRequestLoginSubUser : MessageRequest
{
	[Key(1)]
	public int UserId { get; set; }

	public MessageRequestLoginSubUser() { }

	public MessageRequestLoginSubUser(int userId)
	{
		UserId = userId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

[MessagePackObject]
public class MessageRequestCreateSubUser : MessageRequest
{
	[Key(1)]
	public string Username { get; set; }
	
	[Key(2)]
	public string Email { get; set; }
	
	[Key(3)]
	public string Password { get; set; }
	
	[Key(4)]
	public UserPermissions Permissions { get; set; }

	public MessageRequestCreateSubUser() { }

	public MessageRequestCreateSubUser(string username, string email, string password, UserPermissions permissions)
	{
		Username = username.Trim();
		Email = email.Trim();
		Password = password;
		Permissions = permissions.AddIncluded();
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Username) &&
	                                         !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password);
}

[MessagePackObject]
public class MessageRequestSetOwnerPermissions : MessageRequest
{
	[Key(1)]
	public int UserId { get; set; }
	
	[Key(2)]
	public UserPermissions Permissions { get; set; }

	public MessageRequestSetOwnerPermissions() { }

	public MessageRequestSetOwnerPermissions(int userId, UserPermissions permissions)
	{
		UserId = userId;
		Permissions = permissions;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

[MessagePackObject]
public class MessageRequestResetPassword : MessageRequest
{
	[Key(1)]
	public string Password { get; set; }
	
	[Key(2)]
	public string NewPassword { get; set; }

	public MessageRequestResetPassword() { }

	public MessageRequestResetPassword(string password, string newPassword)
	{
		Password = password;
		NewPassword = newPassword;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Password) && !string.IsNullOrEmpty(NewPassword);
}

[MessagePackObject]
public class MessageRequestListSubUsers : MessageRequest
{
	public MessageRequestListSubUsers() {}
}

[MessagePackObject]
public class MessageRequestCreateVm : MessageRequest
{
	[Key(1)]
	public string Name { get; set; }
	
	[Key(2)]
	public OperatingSystem OperatingSystem { get; set; }
	
	[Key(3)]
	public CpuArchitecture CpuArchitecture { get; set; }
	
	[Key(4)]
	public int RamSizeMiB { get; set; }
	
	[Key(5)]
	public BootMode BootMode { get; set; }

	public MessageRequestCreateVm() { }

	public MessageRequestCreateVm(string name, OperatingSystem operatingSystem, 
		CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
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

[MessagePackObject]
public class MessageRequestDeleteVm : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestDeleteVm() { }

	public MessageRequestDeleteVm(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestListVms : MessageRequest
{
	public MessageRequestListVms() {}
}

[MessagePackObject]
public class MessageRequestCheckVmExist : MessageRequest
{
	[Key(1)]
	public string Name { get; set; }

	public MessageRequestCheckVmExist() { }

	public MessageRequestCheckVmExist(string name)
	{
		Name = name;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name);
}

[MessagePackObject]
public class MessageRequestCreateDriveFs : MessageRequest
{
	[Key(1)]
	public string Name { get; set; }
	
	[Key(2)]
	public int SizeMb { get; set; }
	
	[Key(3)]
	public FileSystemType FileSystem { get; set; }

	public MessageRequestCreateDriveFs() { }

	public MessageRequestCreateDriveFs(string name, int sizeMb, FileSystemType fileSystem)
	{
		Name = name;
		SizeMb = sizeMb;
		FileSystem = fileSystem;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name) 
	                                                               && SizeMb > 0 
	                                                               && SizeMb <= SharedDefinitions.DriveSizeMbMax;
}

[MessagePackObject]
public class MessageRequestCreateDriveFromImage : MessageRequest
{
	[Key(1)]
	public string Name { get; set; }
	
	[Key(2)]
	public DriveType Type { get; set; }
	
	[Key(3)]
	public ulong Size { get; set; }

	public MessageRequestCreateDriveFromImage() { }

	public MessageRequestCreateDriveFromImage(string name, DriveType type, ulong size)
	{
		Name = name;
		Type = type;
		Size = size;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrWhiteSpace(Name) && Enum.IsDefined(typeof(DriveType), Type)
	                                                               && Size / 1024UL / 1024UL <= SharedDefinitions.DriveSizeMbMax;
}

[MessagePackObject]
public class MessageRequestCreateDriveOs : MessageRequest
{
	[Key(1)]
	public string Name { get; set; }
	
	[Key(2)]
	public int SizeMiB { get; set; }
	
	[Key(3)]
	public OperatingSystem OperatingSystem { get; set; }

	public MessageRequestCreateDriveOs() { }

	public MessageRequestCreateDriveOs(string name, int sizeMiB, OperatingSystem operatingSystem)
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

[MessagePackObject]
public class MessageRequestConnectDrive : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public int VmId { get; set; }
	
	public MessageRequestConnectDrive() { }

	public MessageRequestConnectDrive(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestDisconnectDrive : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public int VmId { get; set; }

	public MessageRequestDisconnectDrive() { }

	public MessageRequestDisconnectDrive(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestListDriveConnections : MessageRequest
{
	public MessageRequestListDriveConnections() { }
}

[MessagePackObject]
public class MessageRequestListDrives : MessageRequest
{
	public MessageRequestListDrives() {}
}

[MessagePackObject]
public class MessageRequestListPathItems : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageRequestListPathItems() { }

	public MessageRequestListPathItems(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

[MessagePackObject]
public class MessageRequestDownloadItem : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageRequestDownloadItem() { }

	public MessageRequestDownloadItem(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

[MessagePackObject]
public class MessageRequestUploadFile : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }
	
	[Key(3)]
	public ulong Size { get; set; }

	public MessageRequestUploadFile() { }

	public MessageRequestUploadFile(int driveId, string path, ulong size)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
		Size = size;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && !string.IsNullOrEmpty(Path) &&
	                                         Size > 0 && Size <= SharedDefinitions.DriveSizeMbMax * 1024UL * 1024UL;
}

[MessagePackObject]
public class MessageRequestCreateDirectory : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageRequestCreateDirectory() { }

	public MessageRequestCreateDirectory(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && !string.IsNullOrEmpty(Path);
}

[MessagePackObject]
public class MessageRequestDeleteItem : MessageRequest
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageRequestDeleteItem() { }

	public MessageRequestDeleteItem(int driveId, string path)
	{
		DriveId = driveId;
		Path = Common.CleanPath(path);
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

[MessagePackObject]
public class MessageRequestVmStartup : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestVmStartup() { }

	public MessageRequestVmStartup(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestVmShutdown : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestVmShutdown() { }

	public MessageRequestVmShutdown(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestVmForceOff : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestVmForceOff() { }

	public MessageRequestVmForceOff(int vmId)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestVmStreamStart : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestVmStreamStart() { }

	public MessageRequestVmStreamStart(int vmId)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageRequestVmStreamStop : MessageRequest
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageRequestVmStreamStop() { }

	public MessageRequestVmStreamStop(int vmId)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}