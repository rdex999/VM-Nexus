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

public class MessageRequestDisconnect : MessageRequest
{
	public MessageRequestDisconnect(bool generateGuid)
		: base(generateGuid) {}
}

public class MessageRequestCreateAccount : MessageRequest
{
	public string Username { get; }
	public string Password { get; }

	public MessageRequestCreateAccount(bool generateGuid, string username, string password)
		: base(generateGuid)
	{
		Username = username;
		Password = password;
	}
}