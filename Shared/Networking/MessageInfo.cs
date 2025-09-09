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
	public SharedDefinitions.VmGeneralDescriptor[] VmDescriptors { get; }

	public MessageInfoVmList(bool generateGuid, SharedDefinitions.VmGeneralDescriptor[] vmDescriptors)
		: base(generateGuid)
	{
		VmDescriptors = vmDescriptors;
	}

	public override bool IsValidMessage()
	{
		return base.IsValidMessage() && VmDescriptors != null;	/* Make sure its not null because receiving from a socket. */
	}
}