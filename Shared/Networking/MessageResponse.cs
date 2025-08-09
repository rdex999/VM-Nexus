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

public class MessageResponseConnect : MessageResponse
{
	public bool Accepted { get; }		/* Was the connect request Accepted or Denied by the server? (true: client is connected all good) */

	public MessageResponseConnect(bool generateGuid, Guid requestId, bool accepted)
		: base(generateGuid, requestId)
	{
		Accepted = accepted;
	}
}

public class MessageResponseDisconnect : MessageResponse
{
	public MessageResponseDisconnect(bool generateGuid, Guid requestId)
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
	
	public enum Status
	{
		Success = 0,
		Failure = 1,
		UsernameNotAvailable = 2,
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