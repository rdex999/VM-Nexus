using System.Drawing;
using Avalonia.Input;
using Shared.Drives;
using Shared.VirtualMachines;

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

public class MessageInfoVmCreated : MessageInfoTcp
{
	public VmGeneralDescriptor Descriptor { get; }

	public MessageInfoVmCreated(bool generateGuid, VmGeneralDescriptor descriptor)
		: base(generateGuid)
	{
		Descriptor = descriptor;
	}

	public override bool IsValidMessage() => 
		base.IsValidMessage() && Descriptor != null 
		                      && !string.IsNullOrEmpty(Descriptor.Name) && Descriptor.Id >= 1
		                      && Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), Descriptor.OperatingSystem)
		                      && Enum.IsDefined(typeof(SharedDefinitions.VmState), Descriptor.State);
}

public class MessageInfoVmDeleted : MessageInfoTcp
{
	public int VmId { get; }

	public MessageInfoVmDeleted(bool generateGuid, int vmId)
		: base(generateGuid)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
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

public class MessageInfoDriveCreated : MessageInfoTcp
{
	public DriveGeneralDescriptor Descriptor { get; }

	public MessageInfoDriveCreated(bool generateGuid, DriveGeneralDescriptor descriptor)
		: base(generateGuid)
	{
		Descriptor = descriptor;
	}
}

public class MessageInfoDriveDeleted : MessageInfoTcp
{
	public int DriveId { get; }

	public MessageInfoDriveDeleted(bool generateGuid, int driveId)
		: base(generateGuid)
	{
		DriveId = driveId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

public class MessageInfoDriveConnected : MessageInfoTcp
{
	public int DriveId { get; }
	public int VmId { get; }

	public MessageInfoDriveConnected(bool generateGuid, int driveId, int vmId)
		: base(generateGuid)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}