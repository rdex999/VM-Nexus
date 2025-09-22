using Newtonsoft.Json;

namespace Shared.Networking;

public class MessageResponse : Message
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

	public MessageResponseCreateAccount(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{ 
		Result = result;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
	}

	public enum Status
	{
		Success = 0,
		Failure,
		CredentialsCannotBeEmpty,
		UsernameNotAvailable,
		InvalidUsernameSyntax,
		InvalidEmail
	}
}

public class MessageResponseLogin : MessageResponse
{
	public bool Accepted { get; }

	public MessageResponseLogin(bool generateGuid, Guid requestId, bool accepted)
		: base(generateGuid, requestId)
	{
		Accepted = accepted;
	}
}

public class MessageResponseLogout : MessageResponse
{
	public Status Result { get; }

	public MessageResponseLogout(bool generateGuid, Guid requestId, Status result)
		:  base(generateGuid, requestId)
	{
		Result = result;
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

public class MessageResponseCreateVm : MessageResponse
{
	public Status Result { get; }
	public int Id { get; }
	
	[JsonConstructor]
	public MessageResponseCreateVm(bool generateGuid, Guid requestId, Status result, int id)
		: base(generateGuid, requestId)
	{
		Result = result;
		Id = id;
	}
	public MessageResponseCreateVm(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Id = -1;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (Id >= 1 || Id == -1);

	public enum Status
	{
		Success,
		VmAlreadyExists,
		Failure,
	}
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

public class MessageResponseCreateDrive : MessageResponse
{
	public Status Result { get; }
	public int Id { get; }		/* The ID of the new drive */

	[JsonConstructor]
	public MessageResponseCreateDrive(bool generateGuid, Guid requestId, Status result, int id)
		: base(generateGuid, requestId)
	{
		Result = result;
		Id = id;
	}
	public MessageResponseCreateDrive(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
		Id = -1;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result) && (Id >= 1 || Id == -1);
	
	public enum Status
	{
		Success,
		DriveAlreadyExists,
		Failure,
	}
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

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && Enum.IsDefined(typeof(Status), Result);
	}
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
		Success,
		VmIsShutDown,
		Failure,
	}
}

public class MessageResponseVmScreenStreamStart : MessageResponse
{
	public Status Result { get; }
	public PixelFormat? PixelFormat { get; }

	[JsonConstructor]
	public MessageResponseVmScreenStreamStart(bool generateGuid, Guid requestId, Status result, PixelFormat pixelFormat)
		: base(generateGuid, requestId)
	{
		Result = result;
		PixelFormat = pixelFormat;
	}
	
	public MessageResponseVmScreenStreamStart(bool generateGuid, Guid requestId, Status result)
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

public class MessageResponseVmScreenStreamStop : MessageResponse
{
	public Status Result { get; }

	public MessageResponseVmScreenStreamStop(bool generateGuid, Guid requestId, Status result)
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