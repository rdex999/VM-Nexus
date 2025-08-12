namespace Shared.Networking;

public class MessageRequest : Message
{
	public MessageRequest(bool generateGuid)
		: base(generateGuid) {}
}

public class MessageRequestCheckUsername : MessageRequest	/* Check if the provided username is available (that there is no such user) */
{
	public string Username { get; }

	public MessageRequestCheckUsername(bool generateGuid, string username)
		: base(generateGuid)
	{
		Username = username;
	}
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

public class MessageRequestLogin : MessageRequest
{
	public string Username { get; }
	public string Password { get; }

	public MessageRequestLogin(bool generateGuid, string username, string password)
		: base(generateGuid)
	{
		Username = username;
		Password = password;
	}
}

public class MessageRequestLogout : MessageRequest
{
	public MessageRequestLogout(bool generateGuid)
		: base(generateGuid)
	{
	}
}