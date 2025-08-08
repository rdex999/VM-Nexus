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

public class MessageResponseCreateAccount : MessageResponse
{
	public bool Success { get; }

	public MessageResponseCreateAccount(bool generateGuid, Guid requestId, bool success)
		: base(generateGuid, requestId)
	{
		Success = success;
	}
}