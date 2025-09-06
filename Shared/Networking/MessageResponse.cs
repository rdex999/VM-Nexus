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
	
	public enum Status
	{
		Success = 0,
		Failure,
		CredentialsCannotBeEmpty,
		UsernameNotAvailable,
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
		
	public MessageResponseCreateVm(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
	}

	public enum Status
	{
		Success,
		Failure,
	}
}

public class MessageResponseCreateDrive : MessageResponse
{
	public Status Result { get; }

	public MessageResponseCreateDrive(bool generateGuid, Guid requestId, Status result)
		: base(generateGuid, requestId)
	{
		Result = result;
	}
	
	public enum Status
	{
		Success,
		AlreadyExistsWithName,
		InvalidSize,
	}
}