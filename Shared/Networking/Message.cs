using Newtonsoft.Json;

namespace Shared.Networking;

public interface IMessage
{
	public Guid Id { get; }

	public bool IsValidMessage();
}

public interface IMessageTcp : IMessage {}
public interface IMessageUdp : IMessage {}


public abstract class Message : IMessage
{
	public Guid Id { get; }

	protected Message()
	{
		Id = Guid.NewGuid();
	}
	
	[JsonConstructor]
	protected Message(Guid id)
	{
		Id = id;
	}
	
	public virtual bool IsValidMessage() => Id != Guid.Empty;
}