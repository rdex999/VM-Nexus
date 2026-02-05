using Newtonsoft.Json;
using Shared.Drives;
using Shared.VirtualMachines;

namespace Shared.Networking;

public interface IMessageResponse : IMessageTcp
{
	public Guid RequestId { get; }		/* This is a response for request ID=... */
}

public abstract class MessageResponse : Message, IMessageResponse
{
	public Guid RequestId { get; }
	
	public MessageResponse(Guid requestId)
	{
		RequestId = requestId;
	}

	[JsonConstructor]
	protected MessageResponse(Guid id, Guid requestId)
		: base(id)
	{
		RequestId = requestId;
	}
}

public class MessageResponseInvalidRequestData : MessageResponse	/* If the received request is invalid, this is the response. (haha) */
{
	public MessageResponseInvalidRequestData(Guid requestId) : base(requestId) { }
	
	[JsonConstructor]
	private MessageResponseInvalidRequestData(Guid id, Guid requestId) : base(id, requestId) { }
}

public class MessageResponseCheckUsername : MessageResponse
{
	public bool Available { get; }

	public MessageResponseCheckUsername(Guid requestId, bool available)
		: base(requestId)
	{
		Available = available;
	}
	
	[JsonConstructor]
	private MessageResponseCheckUsername(Guid id, Guid requestId, bool available)
		: base(id, requestId)
	{
		Available = available;
	}
}

public class MessageResponseCreateAccount : MessageResponse
{
	public Status Result { get; }
	public User? User { get; }
	
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
	
	[JsonConstructor]
	private MessageResponseCreateAccount(Guid id, Guid requestId, Status result, User user)
		: base(id, requestId)
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

public class MessageResponseDeleteAccount : MessageResponse
{
	public bool Deleted { get; }

	public MessageResponseDeleteAccount(Guid requestId, bool deleted)
		: base(requestId)
	{
		Deleted = deleted;
	}
	
	[JsonConstructor]
	private MessageResponseDeleteAccount(Guid id, Guid requestId, bool deleted)
		: base(id, requestId)
	{
		Deleted = deleted;
	}
}

public class MessageResponseLogin : MessageResponse
{
	public Status Result { get; }
	public User? User { get; }
	public TimeSpan LoginBlock { get; }

	
	/* Successful login. */
	public MessageResponseLogin(Guid requestId, User user)
		: base(requestId)
	{
		Result = Status.Success;
		User = user;
		LoginBlock = TimeSpan.Zero;
	}
	
	/* Login failed. */
	public MessageResponseLogin(Guid requestId)
		: base(requestId)
	{
		Result = Status.Failure;
		User = null;
		LoginBlock = TimeSpan.MaxValue;
	}
	
	/* Login failed. (blocked) */
	public MessageResponseLogin(Guid requestId, TimeSpan loginBlock)		
		: base(requestId)
	{
		Result = Status.Blocked;
		User = null;
		LoginBlock = loginBlock;
	}

	[JsonConstructor]
	private MessageResponseLogin(Guid id, Guid requestId, Status result, User user, TimeSpan loginBlock)
		: base(id, requestId)
	{
		Result = result;
		User = user;
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

public class MessageResponseLogout : MessageResponse
{
	public Status Result { get; }
	public User? User { get; }

	public MessageResponseLogout(Guid requestId, Status result)
		:  base(requestId)
	{
		Result = result;
		User = null;
	}
	
	public MessageResponseLogout(Guid requestId, Status result, User? user)
		:  base(requestId)
	{
		Result = result;
		User = user;
	}
	
	[JsonConstructor]
	private MessageResponseLogout(Guid id, Guid requestId, Status result, User? user)
		:  base(id, requestId)
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

public class MessageResponseLoginSubUser : MessageResponse
{
	public bool Success => SubUser != null;
	public SubUser? SubUser { get; }

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

	[JsonConstructor]
	private MessageResponseLoginSubUser(Guid id, Guid requestId, SubUser subUser)
		: base(id, requestId)
	{
		SubUser = subUser;
	}
}

public class MessageResponseCreateSubUser : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateSubUser(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	[JsonConstructor]
	private MessageResponseCreateSubUser(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseResetPassword(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseResetPassword(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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
	
	[JsonConstructor]
	private MessageResponseListSubUsers(Guid id, Guid requestId, Status result, SubUser[]? users)
		: base(id, requestId)
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

public class MessageResponseCreateVm : MessageResponse
{
	public Status Result { get; }
	public int VmId { get; }

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
	
	[JsonConstructor]
	private MessageResponseCreateVm(Guid id, Guid requestId, Status result, int vmId)
		: base(id, requestId)
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

public class MessageResponseDeleteVm : MessageResponse
{
	public Status Result { get; }

	public MessageResponseDeleteVm(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseDeleteVm(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	[JsonConstructor]
	private MessageResponseListVms(Guid id, Guid requestId, Status result, VmGeneralDescriptor[]? vms)
		: base(id, requestId)
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

public class MessageResponseCheckVmExist : MessageResponse
{
	public bool Exists { get; }

	public MessageResponseCheckVmExist(Guid requestId, bool exists)
		: base(requestId)
	{
		Exists = exists;
	}
	
	[JsonConstructor]
	private MessageResponseCheckVmExist(Guid id, Guid requestId, bool exists)
		: base(id, requestId)
	{
		Exists = exists;
	}
}

public class MessageResponseCreateDriveFs : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateDriveFs(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseCreateDriveFs(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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
	
	[JsonConstructor]
	private MessageResponseCreateDriveFromImage(Guid id, Guid requestId, Status result, Guid imageTransferId)
		: base(id, requestId)
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

public class MessageResponseCreateDriveOs : MessageResponse
{
	public Status Result { get; }
	public int DriveId { get; }		/* The ID of the new drive */

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

	[JsonConstructor]
	private MessageResponseCreateDriveOs(Guid id, Guid requestId, Status result, int driveId)
		: base(id, requestId)
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

public class MessageResponseConnectDrive : MessageResponse
{
	public Status Result { get; }

	public MessageResponseConnectDrive(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseConnectDrive(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseDisconnectDrive(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseDisconnectDrive(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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
	
	[JsonConstructor]
	private MessageResponseListDriveConnections(Guid id, Guid requestId, Status result, DriveConnection[]? connections)
		: base(id, requestId)
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

public class MessageResponseListDrives : MessageResponse
{
	public Status Result { get; }
	public DriveGeneralDescriptor[]? Drives { get; }

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
	
	[JsonConstructor]
	private MessageResponseListDrives(Guid id, Guid requestId, Status result, DriveGeneralDescriptor[]? drives)
		: base(id, requestId)
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

public class MessageResponseListPathItems : MessageResponse
{
	public Status Result { get; }
	public PathItem[]? PathItems { get; }

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
	
	[JsonConstructor]
	private MessageResponseListPathItems(Guid id, Guid requestId, Status result, PathItem[]? pathItems)
		: base(id, requestId)
	{
		Result = result;
		Result = result;
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
	
	[JsonConstructor]
	private MessageResponseDownloadItem(Guid id, Guid requestId, Status result, Guid streamId, ulong itemSize)
		: base(id, requestId)
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

public class MessageResponseUploadFile : MessageResponse		/* Upload from client perspective - client sends file to server. */
{
	public Status Result { get; }
	public Guid StreamId { get; }

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
	
	[JsonConstructor]
	private MessageResponseUploadFile(Guid id, Guid requestId, Status result, Guid streamId)
		: base(id, requestId)
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

public class MessageResponseCreateDirectory : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateDirectory(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}

	[JsonConstructor]
	private MessageResponseCreateDirectory(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseDeleteItem(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseDeleteItem(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseVmStartup(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseVmStartup(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseVmShutdown(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseVmShutdown(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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

	public MessageResponseVmForceOff(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseVmForceOff(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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
	
	[JsonConstructor]
	private MessageResponseVmStreamStart(Guid id, Guid requestId, Status result, PixelFormat pixelFormat)
		: base(id, requestId)
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

public class MessageResponseVmStreamStop : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmStreamStop(Guid requestId, Status result)
		: base(requestId)
	{
		Result = result;
	}
	
	[JsonConstructor]
	private MessageResponseVmStreamStop(Guid id, Guid requestId, Status result)
		: base(id, requestId)
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