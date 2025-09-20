using System.Drawing;
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

public class MessageInfoVmScreenFrame : MessageInfo
{
	public int VmId { get; }
	public Size Size { get; }
	public byte[] Framebuffer { get; }

	public MessageInfoVmScreenFrame(bool generateGuid, int vmId, Size size, byte[] framebuffer)
		: base(generateGuid)
	{
		VmId = vmId;
		Size = size;
		Framebuffer = framebuffer;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 &&
	                                         Size != null && Size.Width > 0 && Size.Height > 0 && 
	                                         Framebuffer != null && Framebuffer.Length > 0;
}