namespace Shared.Networking;

public class MessageResponse : Message
{
	private Guid RequestId { get; }		/* This is a response for request ID=... */
	
	public MessageResponse(MessageSubject subject, Guid requestId)
		: base(subject)
	{
		RequestId = requestId;
	}
}