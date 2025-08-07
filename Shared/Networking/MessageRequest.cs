namespace Shared.Networking;

public class MessageRequest : Message
{
	public MessageRequest(bool generateGuid)
		: base(generateGuid) {}
}

public class MessageRequestConnect : MessageRequest
{
	public MessageRequestConnect(bool generateGuid)
		: base(generateGuid) {}
}