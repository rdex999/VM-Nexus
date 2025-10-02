using System.Drawing;
using Avalonia.Input;

namespace Shared.Networking;

public class MessageInfoTcp : MessageTcp
{
	public MessageInfoTcp(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageInfoUdp : MessageUdp
{
	public MessageInfoUdp(bool generateGuid)
		: base(generateGuid)
	{
	}
}

public class MessageInfoVmList : MessageInfoTcp		/* An updated list of virtual machines that the user has. */
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

public class MessageInfoVmScreenFrame : MessageInfoUdp
{
	public int VmId { get; }
	public Size Size { get; }
	public byte[] CompressedFramebuffer { get; }

	public MessageInfoVmScreenFrame(bool generateGuid, int vmId, Size size, byte[] compressedFramebuffer)
		: base(generateGuid)
	{
		VmId = vmId;
		Size = size;
		CompressedFramebuffer = compressedFramebuffer;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 &&
	                                         Size != null && Size.Width > 0 && Size.Height > 0 && 
	                                         CompressedFramebuffer != null && CompressedFramebuffer.Length > 0;
}

public class MessageInfoVmAudioPacket : MessageInfoUdp
{
	public int VmId { get; }
	public byte[] Packet { get; }

	public MessageInfoVmAudioPacket(bool generateGuid, int vmId, byte[] packet)
		: base(generateGuid)
	{
		VmId = vmId;
		Packet = packet;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Packet != null && Packet.Length > 0;
}

public class MessageInfoVmPoweredOn : MessageInfoTcp
{
	public int VmId { get; }

	public MessageInfoVmPoweredOn(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmPoweredOff : MessageInfoTcp
{
	public int VmId { get; }

	public MessageInfoVmPoweredOff(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmCrashed : MessageInfoTcp
{
	public int VmId { get; }

	public MessageInfoVmCrashed(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoPointerMoved : MessageInfoTcp
{
	public int VmId { get; }
	public Point Position { get; }

	public MessageInfoPointerMoved(bool generateGuid, int vmId, Point position)
		: base(generateGuid)
	{
		VmId = vmId;
		Position = position;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Position.X >= 0 && Position.Y >= 0;
}

public class MessageInfoPointerButtonEvent : MessageInfoTcp
{
	public int VmId { get; }
	public Point Position { get; }
	public int PressedButtons { get; }		/* Flags - from SharedDefinitions.MouseButtons. */

	public MessageInfoPointerButtonEvent(bool generateGuid, int vmId, Point position, int pressedButtons)
		: base(generateGuid)
	{
		VmId = vmId;
		Position = position;
		PressedButtons = pressedButtons;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Position.X >= 0 && Position.Y >= 0;
}

public class MessageInfoKeyboardKeyEvent : MessageInfoTcp
{
	public int VmId { get; }
	public PhysicalKey Key { get; }
	public bool KeyDown { get; }

	public MessageInfoKeyboardKeyEvent(bool generateGuid, int vmId, PhysicalKey key, bool keyDown)
		: base(generateGuid)
	{
		VmId = vmId;
		Key = key;
		KeyDown = keyDown;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Enum.IsDefined(typeof(PhysicalKey), Key);
}