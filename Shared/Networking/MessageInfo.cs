namespace Shared.Networking;

public class MessageInfo : Message
{
	public MessageInfo(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageInfoVmList : MessageInfo	/* An updated list of virtual machines that the user has. */
{
	public SharedDefinitions.VmDescriptor[] VmDescriptors { get; }

	public MessageInfoVmList(bool generateGuid, SharedDefinitions.VmDescriptor[] vmDescriptors)
		: base(generateGuid)
	{
		VmDescriptors = vmDescriptors;
	}
}