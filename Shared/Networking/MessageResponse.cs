using Newtonsoft.Json;
using Shared.Drives;
using Shared.VirtualMachines;

namespace Shared.Networking;

public class MessageResponse : MessageTcp
{
	public Guid RequestId { get; }		/* This is a response for request ID=... */
	
	public MessageResponse(bool generateGuid, Guid requestId)
		: base(generateGuid)
	{
		RequestId = requestId;
	}
}

public class MessageResponseInvalidRequestData : MessageResponse	/* If the received request is invalid, this is the response. (haha) */
{
	public MessageResponseInvalidRequestData(bool generateGuid, Guid requestId)
		: base(generateGuid, requestId)
	{
	}
}

public class MessageResponseCheckUsername : MessageResponse
{
	public bool Available { get; }

	public MessageResponseCheckUsername(bool generateGuid, Guid requestId, bool available)
		: base(generateGuid, requestId)
	{
		Available = available;
	}
}

public class MessageResponseCreateAccount : MessageResponse
{
	public Status Result { get; }
	public User? User { get; }

	[JsonConstructor]
	public MessageResponseCreateAccount(bool generateGuid, Guid requestId, Status result, User user)
		: base(generateGuid, requestId)
	{ 
		Result = result;
		User = user;
	}
	
	public MessageResponseCreateAccount(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{ 
		Result = result;
		User = null;
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

public class MessageResponseDeleteAccount : MessageResponse
{
	public bool Deleted { get; }

	public MessageResponseDeleteAccount(bool generateGuid, Guid requestId, bool deleted)
		: base(generateGuid, requestId)
	{
		Deleted = deleted;
	}
}

public class MessageResponseLogin : MessageResponse
{
	public bool Accepted => User != null;
	public User? User { get; }
	public TimeSpan LoginBlock { get; }

	[JsonConstructor]
	public MessageResponseLogin(bool generateGuid, Guid requestId, User user, TimeSpan loginBlock)
		: base(generateGuid, requestId)
	{
		User = user;
		LoginBlock = loginBlock;
	}
	
	/* Successful login. */
	public MessageResponseLogin(bool generateGuid, Guid requestId, User user)				
		: base(generateGuid, requestId)
	{
		User = user;
		LoginBlock = TimeSpan.Zero;
	}
	
	/* Login failed. (error) */
	public MessageResponseLogin(bool generateGuid, Guid requestId)							
		: base(generateGuid, requestId)
	{
		User = null;
	}
	
	/* Login failed. (blocked) */
	public MessageResponseLogin(bool generateGuid, Guid requestId, TimeSpan loginBlock)		
		: base(generateGuid, requestId)
	{
		User = null;
		LoginBlock = loginBlock;
	}
}

public class MessageResponseLogout : MessageResponse
{
	public Status Result { get; }
	public User? User { get; }

	[JsonConstructor]
	public MessageResponseLogout(bool generateGuid, Guid requestId, Status result, User? user)
		:  base(generateGuid, requestId)
	{
		Result = result;
		User = user;
	}
	
	public MessageResponseLogout(bool generateGuid, Guid requestId, Status result)
		:  base(generateGuid, requestId)
	{
		Result = result;
		User = null;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
	}
	
	public enum Status
	{
		Success,
		UserNotLoggedIn,
		Failure,
	}
}

public class MessageResponseLoginSubUser : MessageResponse
{
	public bool Success => SubUser != null;
	public SubUser? SubUser { get; }

	[JsonConstructor]
	public MessageResponseLoginSubUser(bool generateGuid, Guid requestId, SubUser subUser)
		: base(generateGuid, requestId)
	{
		SubUser = subUser;
	}
	
	public MessageResponseLoginSubUser(bool generateGuid, Guid requestId)
		: base(generateGuid, requestId)
	{
		SubUser = null;
	}
}

public class MessageResponseCreateSubUser : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateSubUser(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseResetPassword : MessageResponse
{
	public Status Result { get; }

	public MessageResponseResetPassword(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseListSubUsers : MessageResponse
{
	public Status Result { get; }
	public SubUser[]? Users { get; }

	[JsonConstructor]
	public MessageResponseListSubUsers(bool generateGuid, Guid requestId, Status result, SubUser[]? users)
		: base(generateGuid, requestId)
	{
		Result = result;
		Users = users;
	}
	
	public MessageResponseListSubUsers(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Users = null;
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

public class MessageResponseCreateVm : MessageResponse
{
	public Status Result { get; }
	public int VmId { get; }
	
	[JsonConstructor]
	public MessageResponseCreateVm(bool generateGuid, Guid requestId, Status result, int vmId)
		: base(generateGuid, requestId)
	{
		Result = result;
		VmId = vmId;
	}
	public MessageResponseCreateVm(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		VmId = -1;
	}

	public enum Status
	{
		Success,
		VmAlreadyExists,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (VmId >= 1 || VmId == -1);
}

public class MessageResponseDeleteVm : MessageResponse
{
	public Status Result { get; }

	public MessageResponseDeleteVm(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseListVms : MessageResponse
{
	public Status Result { get; }
	public VmGeneralDescriptor[]? Vms { get; }

	[JsonConstructor]
	public MessageResponseListVms(bool generateGuid, Guid requestId, Status result, VmGeneralDescriptor[]? vms)
		: base(generateGuid, requestId)
	{
		Result = result;
		Vms = vms;
	}

	public MessageResponseListVms(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Vms = null;
	}
	
	public enum Status
	{
		Success,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

public class MessageResponseCheckVmExist : MessageResponse
{
	public bool Exists { get; }

	public MessageResponseCheckVmExist(bool generateGuid, Guid requestId, bool exists)
		: base(generateGuid, requestId)
	{
		Exists = exists;
	}
}

public class MessageResponseCreateDriveFs : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateDriveFs(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseCreateDriveFromImage : MessageResponse
{
	public Status Result { get; }
	public Guid ImageTransferId { get; }

	[JsonConstructor]
	public MessageResponseCreateDriveFromImage(bool generateGuid, Guid requestId, Status result, Guid imageTransferId)
		: base(generateGuid, requestId)
	{
		Result = result;
		ImageTransferId = imageTransferId;
	}

	public MessageResponseCreateDriveFromImage(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		ImageTransferId = Guid.Empty;
	}
	
	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

public class MessageResponseCreateDriveOs : MessageResponse
{
	public Status Result { get; }
	public int DriveId { get; }		/* The ID of the new drive */

	[JsonConstructor]
	public MessageResponseCreateDriveOs(bool generateGuid, Guid requestId, Status result, int driveId)
		: base(generateGuid, requestId)
	{
		Result = result;
		DriveId = driveId;
	}
	public MessageResponseCreateDriveOs(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		DriveId = -1;
	}

	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (DriveId >= 1 || DriveId == -1);
}

public class MessageResponseConnectDrive : MessageResponse
{
	public Status Result { get; }

	public MessageResponseConnectDrive(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseDisconnectDrive : MessageResponse
{
	public Status Result { get; }

	public MessageResponseDisconnectDrive(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseListDriveConnections : MessageResponse
{
	public Status Result { get; }
	public DriveConnection[]? Connections { get; }

	[JsonConstructor]
	public MessageResponseListDriveConnections(bool generateGuid, Guid requestId, Status result,
		DriveConnection[]? connections)
		: base(generateGuid, requestId)
	{
		Result = result;
		Connections = connections;
	}

	public MessageResponseListDriveConnections(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Connections = null;
	}
	
	public enum Status
	{
		Success,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

public class MessageResponseListDrives : MessageResponse
{
	public Status Result { get; }
	public DriveGeneralDescriptor[]? Drives { get; }

	[JsonConstructor]
	public MessageResponseListDrives(bool generateGuid, Guid requestId, Status result, DriveGeneralDescriptor[]? drives)
		: base(generateGuid, requestId)
	{
		Result = result;
		Drives = drives;
	}

	public MessageResponseListDrives(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Drives = null;
	}
	
	public enum Status
	{
		Success,
		Failure
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

public class MessageResponseListPathItems : MessageResponse
{
	public Status Result { get; }
	public PathItem[]? PathItems { get; }

	[JsonConstructor]
	public MessageResponseListPathItems(bool generateGuid, Guid requestId, Status result, PathItem[]? pathItems)
		: base(generateGuid, requestId)
	{
		Result = result;
		Result = result;
		PathItems = pathItems;
	}

	public MessageResponseListPathItems(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		PathItems = null;
	}

	public MessageResponseListPathItems(bool generateGuid, Guid requestId, PathItem[] pathItems)
		: base(generateGuid, requestId)
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

public class MessageResponseDownloadItem : MessageResponse		/* Download from client perspective - client receives the item from the server. */
{
	public Status Result { get; }
	public Guid StreamId { get; }
	public ulong ItemSize { get; }

	[JsonConstructor]
	public MessageResponseDownloadItem(bool generateGuid, Guid requestId, Status result, Guid streamId, ulong itemSize)
		: base(generateGuid, requestId)
	{
		Result = result;
		StreamId = streamId;
		ItemSize = itemSize;
	}

	public MessageResponseDownloadItem(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		StreamId = Guid.Empty;
		ItemSize = ulong.MaxValue;
	}
	
	public enum Status
	{
		Success,
		NoSuchItem,
		Failure,
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
}

public class MessageResponseUploadFile : MessageResponse		/* Upload from client perspective - client sends file to server. */
{
	public Status Result { get; }
	public Guid StreamId { get; }

	[JsonConstructor]
	public MessageResponseUploadFile(bool generateGuid, Guid requestId, Status result, Guid streamId)
		: base(generateGuid, requestId)
	{
		Result = result;
		StreamId = streamId;
	}

	public MessageResponseUploadFile(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		StreamId = Guid.Empty;
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

public class MessageResponseCreateDirectory : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateDirectory(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseDeleteItem : MessageResponse
{
	public Status Result { get; }

	public MessageResponseDeleteItem(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseVmStartup : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmStartup(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseVmShutdown : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmShutdown(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseVmForceOff : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmForceOff(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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

public class MessageResponseVmStreamStart : MessageResponse
{
	public Status Result { get; }
	public PixelFormat? PixelFormat { get; }

	[JsonConstructor]
	public MessageResponseVmStreamStart(bool generateGuid, Guid requestId, Status result, PixelFormat pixelFormat)
		: base(generateGuid, requestId)
	{
		Result = result;
		PixelFormat = pixelFormat;
	}
	
	public MessageResponseVmStreamStart(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		PixelFormat = null;
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

public class MessageResponseVmStreamStop : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmStreamStop(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
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