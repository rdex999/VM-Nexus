namespace Shared.Networking;

public class Message
{
	private Guid Id { get; }
	private MessageSubject Subject { get; }

	public Message(MessageSubject subject)
	{
		Subject = subject;
		Id = Guid.NewGuid();
	}
}

/* Prefix with "Req" for "Request" and "Res" for Response */
public enum MessageSubject
{
	ReqConnect,
	ResConnectAllowed,
	ResConnectDenied,
}