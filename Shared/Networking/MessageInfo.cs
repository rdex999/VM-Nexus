namespace Shared.Networking;

/* Sending quick info from side to side - no responses for MessageInfo */
public class MessageInfo : Message
{
	public MessageInfo(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageInfoDisconnect : MessageInfo
{
	public MessageInfoDisconnect(bool generateGuid)
		: base(generateGuid)
	{
	}
}