namespace Shared.Networking;

public class MessageResponse : Message
{
	private Guid RequestId { get; }		/* This is a response for request ID=... */
	
	public MessageResponse(Guid requestId)
	{
		RequestId = requestId;
	}
}

public class MessageResponseConnect : MessageResponse
{
	public bool Accepted { get; }		/* Was the connect request Accepted or Denied by the server? (true: client is connected all good) */

	public MessageResponseConnect(Guid requestId, bool accepted)
		: base(requestId)
	{
		Accepted = accepted;
	}
}