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
	public string Email { get; }
	public string Password { get; }

	public MessageRequestCreateAccount(bool generateGuid, string username, string email, string password)
		: base(generateGuid)
	{
		Username = username;
		Email = email;
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

public class MessageRequestCreateVm : MessageRequest
{
	public string Name { get; }
	public SharedDefinitions.OperatingSystem OperatingSystem { get; }
	public SharedDefinitions.CpuArchitecture CpuArchitecture { get; }
	public SharedDefinitions.BootMode BootMode { get; }

	public MessageRequestCreateVm(bool generateGuid, string name,
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
		: base(generateGuid)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		BootMode = bootMode;
	}
}

public class MessageRequestCheckVmExist : MessageRequest	/* Check if there is a virtual machine with the given name */
{
	public string Name { get; }

	public MessageRequestCheckVmExist(bool generateGuid, string name)
		: base(generateGuid)
	{
		Name = name;
	}
}

public class MessageRequestCreateDrive : MessageRequest
{
	public string Name { get; }
	public SharedDefinitions.DriveType Type { get; }
	public int Size { get; }	/* The size of the drive - in MiB */
	
	public MessageRequestCreateDrive(bool generateGuid,  string name, SharedDefinitions.DriveType type, int size)
		: base(generateGuid)
	{
		Name = name;
		Type = type;
		Size = size;
	}
}