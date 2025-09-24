using System.Drawing;
using Avalonia.Input;

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

public class MessageInfoVmPoweredOn : MessageInfo
{
	public int VmId { get; }

	public MessageInfoVmPoweredOn(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmPoweredOff : MessageInfo
{
	public int VmId { get; }

	public MessageInfoVmPoweredOff(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmCrashed : MessageInfo
{
	public int VmId { get; }

	public MessageInfoVmCrashed(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoPointerMoved : MessageInfo
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

public class MessageInfoPointerButtonEvent : MessageInfo
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

public class MessageInfoKeyboardKeyEvent : MessageInfo
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