namespace Shared.Networking;

public class Message
{
	public Guid Id { get; }

	public Message()
	{
		Id = Guid.NewGuid();
	}
}