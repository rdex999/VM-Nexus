namespace Shared.Networking;

public class Message
{
	public Guid Id { get; set; }

	public Message(bool generateGuid)
	{
		if (generateGuid)
			Id = Guid.NewGuid();
	}

	public virtual bool IsValidMessage()
	{
		return true; 
	}
}

public class MessageTcp : Message
{
	public MessageTcp(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageUdp : Message
{
	public MessageUdp(bool generateGuid)
		: base(generateGuid)
	{
	}
}