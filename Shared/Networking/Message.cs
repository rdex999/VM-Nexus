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