using Newtonsoft.Json;

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
	public SharedDefinitions.OperatingSystem OperatingSystem { get; }
	public SharedDefinitions.CpuArchitecture CpuArchitecture { get; }
	public SharedDefinitions.BootMode BootMode { get; }

	public MessageRequestCreateVm(bool generateGuid, string name,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
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
		       Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), OperatingSystem) &&
		       Enum.IsDefined(typeof(SharedDefinitions.CpuArchitecture), CpuArchitecture) &&
		       Enum.IsDefined(typeof(SharedDefinitions.BootMode), BootMode);
	}
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

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && !string.IsNullOrEmpty(Name);
	}
}

public class MessageRequestCreateDrive : MessageRequest
{
	public string Name { get; }
	public SharedDefinitions.DriveType Type { get; }
	public int Size { get; }	/* The size of the drive - in MiB */
	
	/*
	 * If other is selected or -1 - an operating system will not be installed on the drive.
	 * If an operating system is selected, then the FilesystemType, PartitionTableType, Partitions properties are ignored.
	 */
	public SharedDefinitions.OperatingSystem OperatingSystem { get; }			/* Can be -1 for no operating system. */
	public SharedDefinitions.FilesystemType FilesystemType { get; }				/* Can be -1. Used only if no partition table is used. (PartitionTableType must be -1) */
	public SharedDefinitions.PartitionTableType PartitionTableType { get; }		/* When used, FilesystemType should be -1 (not used). */
	public SharedDefinitions.PartitionDescriptor[] Partitions { get; }			/* Can be empty if using a filesystem only. */


	public MessageRequestCreateDrive(bool generateGuid, string name, SharedDefinitions.DriveType type, int size,
		SharedDefinitions.OperatingSystem operatingSystem)
		: base(generateGuid)
	{
		Name = name;
		Type = type;
		Size = size;
		OperatingSystem = operatingSystem;
		Partitions = [];
		
		FilesystemType = (SharedDefinitions.FilesystemType)(-1);
		PartitionTableType = (SharedDefinitions.PartitionTableType)(-1);
	}
	
	public MessageRequestCreateDrive(bool generateGuid, string name, SharedDefinitions.DriveType type, int size,
		SharedDefinitions.PartitionTableType partitionTableType, SharedDefinitions.PartitionDescriptor[] partitions)
		: base(generateGuid)
	{
		Name = name;
		Type = type;
		Size = size;
		PartitionTableType = partitionTableType;
		Partitions = partitions;
		FilesystemType = (SharedDefinitions.FilesystemType)(-1);
		OperatingSystem = (SharedDefinitions.OperatingSystem)(-1);
	}

	[JsonConstructor]
	public MessageRequestCreateDrive(bool generateGuid, string name, SharedDefinitions.DriveType type, int size,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.FilesystemType filesystemType,
		SharedDefinitions.PartitionTableType partitionTableType, SharedDefinitions.PartitionDescriptor[] partitions)
		: base(generateGuid)
	{
		Name = name;
		Type = type;
		Size = size;
		OperatingSystem = operatingSystem;
		FilesystemType = filesystemType;
		PartitionTableType = partitionTableType;
		Partitions = partitions;
	}

	public override bool IsValidMessage()
	{
		if (!base.IsValidMessage() || string.IsNullOrEmpty(Name) || !Enum.IsDefined(typeof(SharedDefinitions.DriveType), Type) || Size < 1)
		{
			return false;
		}

		if (!Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), OperatingSystem) &&
		    OperatingSystem != (SharedDefinitions.OperatingSystem)(-1))
		{
			return false;
		}

		if (Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), OperatingSystem) &&
		    OperatingSystem != SharedDefinitions.OperatingSystem.Other)
		{
			if (FilesystemType != (SharedDefinitions.FilesystemType)(-1) ||
			    PartitionTableType != (SharedDefinitions.PartitionTableType)(-1) || Partitions.Length != 0)
			{
				return false;
			}
		}
		else if (OperatingSystem == SharedDefinitions.OperatingSystem.Other ||
		         OperatingSystem == (SharedDefinitions.OperatingSystem)(-1))
		{
			if (Enum.IsDefined(typeof(SharedDefinitions.FilesystemType), FilesystemType) &&
			    PartitionTableType != (SharedDefinitions.PartitionTableType)(-1))
			{
				return false;
			}

			if (Enum.IsDefined(typeof(SharedDefinitions.PartitionTableType), PartitionTableType) &&
			    (FilesystemType != (SharedDefinitions.FilesystemType)(-1) || Partitions.Length == 0))
			{
				return false;
			}
		}

		return true;
	}
}

public class MessageRequestDeleteDrive : MessageRequest
{
	public int DriveId { get; }

	public MessageRequestDeleteDrive(bool generateGuid, int driveId)
		: base(generateGuid)
	{
		DriveId = driveId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
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

public class MessageRequestListConnectedDrives : MessageRequest		/* Request a list of drives connected to the given VM */
{
	public int VmId { get; }

	public MessageRequestListConnectedDrives(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
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