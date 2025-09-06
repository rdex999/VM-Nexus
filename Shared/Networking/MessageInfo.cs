namespace Shared.Networking;

public class MessageInfo
{
	
}

public class MessageInfoVmList : MessageInfo	/* An updated list of virtual machines that the user has. */
{
	public SharedDefinitions.VmDescriptor[] VmDescriptors { get; }

	public MessageInfoVmList(SharedDefinitions.VmDescriptor[] vmDescriptors)
	{
		VmDescriptors = vmDescriptors;
	}
}