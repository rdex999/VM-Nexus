using MessagePack;
using Shared.Drives;
using Shared.VirtualMachines;

namespace Shared.Networking;

public interface IMessageResponse : IMessageTcp
{
	public Guid RequestId { get; }
}

public abstract class MessageResponse : Message, IMessageResponse
{
	[Key(1)] 
	public Guid RequestId { get; set; } = Guid.Empty;
	
	public MessageResponse() { }

	public MessageResponse(Guid requestId)
	{
		RequestId = requestId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && RequestId != Guid.Empty;
}

[MessagePackObject]
public class MessageResponseInvalidRequestData : MessageResponse
{
	public MessageResponseInvalidRequestData(Guid requestId) : base(requestId) { }
}

[MessagePackObject]
public class MessageResponseCheckUsername : MessageResponse
{
	[Key(2)]
	public bool Available { get; set; }

	public MessageResponseCheckUsername() { }

	public MessageResponseCheckUsername(Guid requestId, bool available)
		: base(requestId)
	{
		Available = available;
	}
}

[MessagePackObject]
public class MessageResponseCreateAccount : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public User? User { get; set; } 
	
	public MessageResponseCreateAccount() { }
	
	public MessageResponseCreateAccount(Guid requestId, Status result)
		: base(requestId)
	{ 
		Result = result;
		User = null;
	}
	
	public MessageResponseCreateAccount(Guid requestId, Status result, User user)
		: base(requestId)
	{ 
		Result = result;
		User = user;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);

	public enum Status
	{
		Success = 0,
		UsernameNotAvailable,
		InvalidUsernameSyntax,
		InvalidEmail,
		Failure,
	}
}

[MessagePackObject]
public class MessageResponseDeleteAccount : MessageResponse
{
	[Key(2)]
	public bool Deleted { get; set; }

	public MessageResponseDeleteAccount() { }

	public MessageResponseDeleteAccount(Guid requestId, bool deleted)
		: base(requestId)
	{
		Deleted = deleted;
	}
}

[MessagePackObject]
public class MessageResponseLogin : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public User? User { get; set; }
	
	[Key(4)]
	public TimeSpan LoginBlock { get; set; }

	public MessageResponseLogin(Guid requestId, User user)
		: base(requestId)
	{
		Result = Status.Success;
		User = user;
		LoginBlock = TimeSpan.Zero;
	}
	
	public MessageResponseLogin(Guid requestId)
		: base(requestId)
	{
		Result = Status.Failure;
		User = null!;
		LoginBlock = TimeSpan.MaxValue;
	}
	
	public MessageResponseLogin(Guid requestId, TimeSpan loginBlock)		
		: base(requestId)
	{
		Result = Status.Blocked;
		User = null!;
		LoginBlock = loginBlock;
	}

	public enum Status
	{
		Success,
		Blocked,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseLogout : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public User? User { get; set; }

	public MessageResponseLogout() { }

	public MessageResponseLogout(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		User = null;
	}
	
	public MessageResponseLogout(Guid requestId, Status result, User user)
		: base(requestId)
	{
		Result = result;
		User = user;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
	
	public enum Status
	{
		Success,
		UserNotLoggedIn,
		Failure,
	}
}

[MessagePackObject]
public class MessageResponseLoginSubUser : MessageResponse
{
	[Key(2)] 
	public SubUser? SubUser { get; set; }

	[Key(3)]
	public bool Success => SubUser != null;

	public MessageResponseLoginSubUser(Guid requestId)
		: base(requestId)
	{
		SubUser = null;
	}
	
	public MessageResponseLoginSubUser(Guid requestId, SubUser subUser)
		: base(requestId)
	{
		SubUser = subUser;
	}
}

[MessagePackObject]
public class MessageResponseCreateSubUser : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseCreateSubUser() { }

	public MessageResponseCreateSubUser(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		UsernameNotAvailable,
		InvalidUsernameSyntax,
		InvalidEmail,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseSetOwnerPermissions : MessageResponse
{
	[Key(2)]
	public bool Success { get; set; }

	public MessageResponseSetOwnerPermissions() { }

	public MessageResponseSetOwnerPermissions(Guid requestId, bool success)
		: base(requestId)
	{
		Success = success;
	}
}

[MessagePackObject]
public class MessageResponseResetPassword : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseResetPassword() { }

	public MessageResponseResetPassword(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		InvalidPassword,
		Failure
	}
}

[MessagePackObject]
public class MessageResponseListSubUsers : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public SubUser[]? Users { get; set; }

	public MessageResponseListSubUsers() { }

	public MessageResponseListSubUsers(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		Users = null;
	}
	
	public MessageResponseListSubUsers(Guid requestId, Status result, SubUser[] users)
		: base(requestId)
	{
		Result = result;
		Users = users;
	}
	
	public enum Status
	{
		Success,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) 
	                                                               && ((Result == Status.Success && Users != null) 
	                                                                   || (Result != Status.Success && Users == null));
}

[MessagePackObject]
public class MessageResponseCreateVm : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	
	[Key(3)]
	public int VmId { get; set; }

	public MessageResponseCreateVm() { }

	public MessageResponseCreateVm(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		VmId = -1;
	}
	
	public MessageResponseCreateVm(Guid requestId, Status result, int vmId)
		: base(requestId)
	{
		Result = result;
		VmId = vmId;
	}
	
	public enum Status
	{
		Success,
		VmAlreadyExists,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (VmId >= 1 || VmId == -1);
}

[MessagePackObject]
public class MessageResponseDeleteVm : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseDeleteVm() { }

	public MessageResponseDeleteVm(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		VirtualMachineIsRunning,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseListVms : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	
	[Key(3)]
	public VmGeneralDescriptor[]? Vms { get; set; }

	public MessageResponseListVms() { }

	public MessageResponseListVms(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		Vms = null;
	}
	
	public MessageResponseListVms(Guid requestId, Status result, VmGeneralDescriptor[] vms)
		: base(requestId)
	{
		Result = result;
		Vms = vms;
	}

	public enum Status
	{
		Success,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseCheckVmExist : MessageResponse
{
	[Key(2)]
	public bool Exists { get; set; }

	public MessageResponseCheckVmExist() { }

	public MessageResponseCheckVmExist(Guid requestId, bool exists)
		: base(requestId)
	{
		Exists = exists;
	}
}

[MessagePackObject]
public class MessageResponseCreateDriveFs : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseCreateDriveFs() { }

	public MessageResponseCreateDriveFs(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}
}

[MessagePackObject]
public class MessageResponseCreateDriveFromImage : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	
	[Key(3)]
	public Guid ImageTransferId { get; set; }

	public MessageResponseCreateDriveFromImage() { }

	public MessageResponseCreateDriveFromImage(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		ImageTransferId = Guid.Empty;
	}
	
	public MessageResponseCreateDriveFromImage(Guid requestId, Status result, Guid imageTransferId)
		: base(requestId)
	{
		Result = result;
		ImageTransferId = imageTransferId;
	}
	
	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseCreateDriveOs : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	
	[Key(3)]
	public int DriveId { get; set; }

	public MessageResponseCreateDriveOs() { }

	public MessageResponseCreateDriveOs(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		DriveId = -1;
	}
	
	public MessageResponseCreateDriveOs(Guid requestId, Status result, int driveId)
		: base(requestId)
	{
		Result = result;
		DriveId = driveId;
	}

	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (DriveId >= 1 || DriveId == -1);
}

[MessagePackObject]
public class MessageResponseConnectDrive : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseConnectDrive() { }

	public MessageResponseConnectDrive(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,
		AlreadyConnected,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseDisconnectDrive : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseDisconnectDrive() { }

	public MessageResponseDisconnectDrive(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,
		NotConnected,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseListDriveConnections : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public DriveConnection[]? Connections { get; set; }

	public MessageResponseListDriveConnections() { }

	public MessageResponseListDriveConnections(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		Connections = null;
	}
	
	public MessageResponseListDriveConnections(Guid requestId, Status result, DriveConnection[] connections)
		: base(requestId)
	{
		Result = result;
		Connections = connections;
	}

	public enum Status
	{
		Success,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseListDrives : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	[Key(3)] 
	public DriveGeneralDescriptor[]? Drives { get; set; }

	public MessageResponseListDrives() { }

	public MessageResponseListDrives(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		Drives = null;
	}
	
	public MessageResponseListDrives(Guid requestId, Status result, DriveGeneralDescriptor[] drives)
		: base(requestId)
	{
		Result = result;
		Drives = drives;
	}
	
	public enum Status
	{
		Success,
		Failure
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseListPathItems : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	
	[Key(3)]
	public PathItem[]? PathItems { get; set; }

	public MessageResponseListPathItems() { }
	
	public MessageResponseListPathItems(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		PathItems = null;
	}

	public MessageResponseListPathItems(Guid requestId, PathItem[] pathItems)
		: base(requestId)
	{
		Result = Status.Success;
		PathItems = pathItems;
	}

	public enum Status
	{
		Success,
		InvalidPath,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseDownloadItem : MessageResponse		/* Download from client perspective - client receives the item from the server. */
{
	[Key(2)]
	public Status Result { get; set; }
	[Key(3)]
	public Guid StreamId { get; set; }
	[Key(4)]
	public ulong ItemSize { get; set; }

	public MessageResponseDownloadItem() { }
	
	public MessageResponseDownloadItem(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		StreamId = Guid.Empty;
		ItemSize = ulong.MaxValue;
	}
	
	public MessageResponseDownloadItem(Guid requestId, Status result, Guid streamId, ulong itemSize)
		: base(requestId)
	{
		Result = result;
		StreamId = streamId;
		ItemSize = itemSize;
	}
	
	public enum Status
	{
		Success,
		NoSuchItem,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseUploadFile : MessageResponse		/* Upload from client perspective - client sends file to server. */
{
	[Key(2)]
	public Status Result { get; set; }
	[Key(3)]
	public Guid StreamId { get; set; }

	public MessageResponseUploadFile() { }
	
	public MessageResponseUploadFile(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		StreamId = Guid.Empty;
	}
	
	public MessageResponseUploadFile(Guid requestId, Status result, Guid streamId)
		: base(requestId)
	{
		Result = result;
		StreamId = streamId;
	}
	
	public enum Status
	{
		Success,
		InvalidPath,
		FileTooLarge,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseCreateDirectory : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseCreateDirectory() { }
	
	public MessageResponseCreateDirectory(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		InvalidPath,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseDeleteItem : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseDeleteItem() { }
	
	public MessageResponseDeleteItem(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		NoSuchItem,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseVmStartup : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseVmStartup() { }
	
	public MessageResponseVmStartup(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,
		VmAlreadyRunning,
		ServerStarvation,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseVmShutdown : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseVmShutdown() { }
	
	public MessageResponseVmShutdown(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,		/* Means that a shutdown signal was sent to the virtual machine. Doesn't mean its shutdown. */
		VmIsShutDown,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseVmForceOff : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseVmForceOff() { }
	
	public MessageResponseVmForceOff(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		VmIsShutDown,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

[MessagePackObject]
public class MessageResponseVmStreamStart : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }
	[Key(3)]
	public PixelFormat? PixelFormat { get; set; }
	
	public MessageResponseVmStreamStart() { }
		
	public MessageResponseVmStreamStart(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
		PixelFormat = null;
	}
	
	public MessageResponseVmStreamStart(Guid requestId, Status result, PixelFormat pixelFormat)
		: base(requestId)
	{
		Result = result;
		PixelFormat = pixelFormat;
	}
	
	public enum Status
	{
		Success,
		AlreadyStreaming,
		Failure,
	}
	
	public override bool IsValidMessage() =>  base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && 
	                                          (PixelFormat == null || Enum.IsDefined(typeof(PixelFormat.PixelFormatType), PixelFormat.Type));
}

[MessagePackObject]
public class MessageResponseVmStreamStop : MessageResponse
{
	[Key(2)]
	public Status Result { get; set; }

	public MessageResponseVmStreamStop() { }
	
	public MessageResponseVmStreamStop(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,
		StreamNotRunning,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}