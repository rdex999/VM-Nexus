using Newtonsoft.Json;
using Shared.Drives;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Shared.Networking;

public class MessageRequest : MessageTcp
{
	public MessageRequest(bool generateGuid)
		: base(generateGuid) {}
}

public class MessageRequestCheckUsername : MessageRequest	/* Check if the provided username is available (that there is no such user) */
{
	public string Username { get; }

	public MessageRequestCheckUsername(bool generateGuid, string username)
		: base(generateGuid)
	{
		Username = username;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && !string.IsNullOrEmpty(Username);
	}
}

public class MessageRequestCreateAccount : MessageRequest
{
	public string Username { get; }
	public string Email { get; }
	public string Password { get; }

	public MessageRequestCreateAccount(bool generateGuid, string username, string email, string password)
		: base(generateGuid)
	{
		Username = username;
		Email = email;
		Password = password;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password);
	}
}

public class MessageRequestLogin : MessageRequest
{
	public string Username { get; }
	public string Password { get; }

	public MessageRequestLogin(bool generateGuid, string username, string password)
		: base(generateGuid)
	{
		Username = username;
		Password = password;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
	}
}

public class MessageRequestLogout : MessageRequest
{
	public MessageRequestLogout(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageRequestCreateVm : MessageRequest
{
	public string Name { get; }
	public OperatingSystem OperatingSystem { get; }
	public CpuArchitecture CpuArchitecture { get; }
	public BootMode BootMode { get; }

	public MessageRequestCreateVm(bool generateGuid, string name,
		OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, BootMode bootMode)
		: base(generateGuid)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		BootMode = bootMode;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && !string.IsNullOrEmpty(Name) &&
		       Enum.IsDefined(typeof(OperatingSystem), OperatingSystem) &&
		       Enum.IsDefined(typeof(CpuArchitecture), CpuArchitecture) &&
		       Enum.IsDefined(typeof(BootMode), BootMode);
	}
}

public class MessageRequestDeleteVm : MessageRequest
{
	public int VmId { get; }

	public MessageRequestDeleteVm(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestListVms : MessageRequest
{
	public MessageRequestListVms(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageRequestCheckVmExist : MessageRequest	/* Check if there is a virtual machine with the given name */
{
	public string Name { get; }

	public MessageRequestCheckVmExist(bool generateGuid, string name)
		: base(generateGuid)
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

	public MessageRequestCreateDriveFs(bool generateGuid, string name, int sizeMb, FileSystemType fileSystem)
		: base(generateGuid)
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

	public MessageRequestCreateDriveFromImage(bool generateGuid, string name, DriveType type, ulong size)
		: base(generateGuid)
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
	public int Size { get; }	/* The size of the drive - in MiB */
	
	/*
	 * If other is selected or -1 - an operating system will not be installed on the drive.
	 * If an operating system is selected, then the FilesystemType, PartitionTableType, Partitions properties are ignored.
	 */
	public OperatingSystem OperatingSystem { get; }			/* Can be -1 for no operating system. */

	public MessageRequestCreateDriveOs(bool generateGuid, string name, int size, OperatingSystem operatingSystem)
		: base(generateGuid)
	{
		Name = name;
		Size = size;
		OperatingSystem = operatingSystem;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && !string.IsNullOrEmpty(Name) 
	                                                               && Enum.IsDefined(typeof(OperatingSystem), OperatingSystem)
	                                                               && OperatingSystem != OperatingSystem.Other
	                                                               && Common.IsOperatingSystemDriveSizeValid(OperatingSystem, Size);
}

public class MessageRequestConnectDrive : MessageRequest	/* Request to mark the drive as connected to some VM. */
{
	public int DriveId { get; }
	public int VmId { get; }
	
	public MessageRequestConnectDrive(bool generateGuid, int driveId, int vmId)
		: base(generateGuid)
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

	public MessageRequestDisconnectDrive(bool generateGuid, int driveId, int vmId)
		: base(generateGuid)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

public class MessageRequestListDriveConnections : MessageRequest
{
	public MessageRequestListDriveConnections(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageRequestListDrives : MessageRequest
{
	public MessageRequestListDrives(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageRequestListPathItems : MessageRequest
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestListPathItems(bool generateGuid, int driveId, string path)
		: base(generateGuid)
	{
		DriveId = driveId;
		Path = path;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestDownloadItem : MessageRequest	/* Download from client perspective - client receives the item from the server. */
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestDownloadItem(bool generateGuid, int driveId, string path)
		: base(generateGuid)
	{
		DriveId = driveId;
		Path = path;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestUploadFile : MessageRequest		/* Upload from client perspective - client sends the file to the server. */
{
	public int DriveId { get; }
	public string Path { get; }		/* Destination inside the drive. Includes the filename. */
	public ulong Size { get; }		/* In bytes. */

	public MessageRequestUploadFile(bool generateGuid, int driveId, string path, ulong size)
		: base(generateGuid)
	{
		DriveId = driveId;
		Path = path;
		Size = size;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && !string.IsNullOrEmpty(Path) &&
	                                         Size > 0 && Size <= SharedDefinitions.DriveSizeMbMax * 1024UL * 1024UL;
}

public class MessageRequestDeleteItem : MessageRequest
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageRequestDeleteItem(bool generateGuid, int driveId, string path)
		: base(generateGuid)
	{
		DriveId = driveId;
		Path = path;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageRequestVmStartup : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmStartup(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmShutdown : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmShutdown(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmForceOff : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmForceOff(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmStreamStart : MessageRequest		/* Request to send a video stream of the screen of a virtual machine (through MessageInfo) */
{
	public int VmId { get; }

	public MessageRequestVmStreamStart(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageRequestVmStreamStop : MessageRequest
{
	public int VmId { get; }

	public MessageRequestVmStreamStop(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}