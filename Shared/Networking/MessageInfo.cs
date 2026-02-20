using System.Drawing;
using System.Net;
using Avalonia.Input;
using MessagePack;
using Shared.Drives;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Shared.Networking;

public interface IMessageInfo : IMessage {}

public abstract class MessageInfo : Message, IMessageInfo
{
	protected MessageInfo() {}
}

[MessagePackObject]
public class MessageInfoIdentifyUdpPort : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int Port { get; set; }

	public MessageInfoIdentifyUdpPort() { }

	public MessageInfoIdentifyUdpPort(int port)
	{
		Port = port;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && Port >= IPEndPoint.MinPort && Port <= IPEndPoint.MaxPort;
}

[MessagePackObject]
public class MessageInfoCryptoUdp : MessageInfo, IMessageTcp
{
	[Key(1)]
	public byte[] MasterKey32 { get; set; }
	
	[Key(2)]
	public byte[] Salt32 { get; set; }
	
	public MessageInfoCryptoUdp() { }
	
	public MessageInfoCryptoUdp(byte[] masterKey32, byte[] salt32)
	{
		MasterKey32 = masterKey32;
		Salt32 = salt32;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && MasterKey32.Length == 32 && Salt32.Length == 32;
}

[MessagePackObject]
public class MessageInfoSubUserCreated : MessageInfo, IMessageTcp
{
	[Key(1)]
	public SubUser SubUser { get; set; }

	public MessageInfoSubUserCreated() { }

	public MessageInfoSubUserCreated(SubUser subUser)
	{
		SubUser = subUser;
	}
}

[MessagePackObject]
public class MessageInfoUserData : MessageInfo, IMessageTcp
{
	[Key(1)]
	public User User { get; set; }

	public MessageInfoUserData() { }

	public MessageInfoUserData(User user)
	{
		User = user;
	}
}

[MessagePackObject]
public class MessageInfoUserDeleted : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int UserId { get; set; }

	public MessageInfoUserDeleted() { }

	public MessageInfoUserDeleted(int userId)
	{
		UserId = userId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

[MessagePackObject]
public class MessageInfoVmCreated : MessageInfo, IMessageTcp
{
	[Key(1)]
	public VmGeneralDescriptor Descriptor { get; set; }

	public MessageInfoVmCreated() { }

	public MessageInfoVmCreated(VmGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}

	public override bool IsValidMessage() => base.IsValidMessage() 
	                                         && !string.IsNullOrEmpty(Descriptor.Name) && Descriptor.Id >= 1
	                                         && Enum.IsDefined(typeof(OperatingSystem), Descriptor.OperatingSystem)
	                                         && Enum.IsDefined(typeof(VmState), Descriptor.State);
}

[MessagePackObject]
public class MessageInfoVmDeleted : MessageInfo, IMessageTcp 
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageInfoVmDeleted() { }

	public MessageInfoVmDeleted(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoVmScreenFrame : MessageInfo, IMessageUdp
{
	[Key(1)]
	public int VmId { get; set; }
	
	[IgnoreMember]
	public Size Size => new Size(SizeWidth, SizeHeight);
	
	[Key(2)]
	public int SizeWidth { get; set; }
	
	[Key(3)]
	public int SizeHeight { get; set; }
	
	[Key(4)]
	public byte[] CompressedFramebuffer { get; set; }
	
	public MessageInfoVmScreenFrame() { }

	public MessageInfoVmScreenFrame(int vmId, Size size, byte[] compressedFramebuffer)
	{
		VmId = vmId;
		SizeWidth = size.Width;
		SizeHeight = size.Height;
		CompressedFramebuffer = compressedFramebuffer;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 &&
	                                         SizeWidth > 0 && SizeHeight > 0 && 
	                                         CompressedFramebuffer.Length > 0;
}

[MessagePackObject]
public class MessageInfoVmAudioPacket : MessageInfo, IMessageUdp
{
	[Key(1)]
	public int VmId { get; set; }
	
	[Key(2)]
	public byte[] Packet { get; set; }

	public MessageInfoVmAudioPacket() { }

	public MessageInfoVmAudioPacket(int vmId, byte[] packet)
	{
		VmId = vmId;
		Packet = packet;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Packet != null && Packet.Length > 0;
}

[MessagePackObject]
public class MessageInfoVmPoweredOn : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageInfoVmPoweredOn() { }

	public MessageInfoVmPoweredOn(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoVmPoweredOff : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageInfoVmPoweredOff() { }

	public MessageInfoVmPoweredOff(int vmId)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoVmCrashed : MessageInfo, IMessageTcp 
{
	[Key(1)]
	public int VmId { get; set; }

	public MessageInfoVmCrashed() { }

	public MessageInfoVmCrashed(int vmId)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoPointerMoved : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int VmId { get; set; }
	
	[IgnoreMember]
	public Point Position => new Point(PositionX, PositionY);

	[Key(2)]
	public int PositionX { get; set; }
	
	[Key(3)]
	public int PositionY { get; set; }

	public MessageInfoPointerMoved() { }

	public MessageInfoPointerMoved(int vmId, Point position)
	{
		VmId = vmId;
		PositionX = position.X;
		PositionY = position.Y;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && PositionX >= 0 && PositionY >= 0;
}

[MessagePackObject]
public class MessageInfoPointerButtonEvent : MessageInfo, IMessageTcp 
{
	[Key(1)]
	public int VmId { get; set; }
	
	[IgnoreMember]
	public Point Position => new Point(PositionX, PositionY);
	
	[Key(2)]
	public int PositionX { get; set; }
	
	[Key(3)]
	public int PositionY { get; set; }
	
	[Key(4)]
	public int PressedButtons { get; set; }

	public MessageInfoPointerButtonEvent() { }

	public MessageInfoPointerButtonEvent(int vmId, Point position, int pressedButtons)
	{
		VmId = vmId;
		PositionX = position.X;
		PositionY = position.Y;
		PressedButtons = pressedButtons;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && PositionX >= 0 && PositionY >= 0;
}

[MessagePackObject]
public class MessageInfoKeyboardKeyEvent : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int VmId { get; set; }
	
	[Key(2)]
	public PhysicalKey Key { get; set; }
	
	[Key(3)]
	public bool KeyDown { get; set; }

	public MessageInfoKeyboardKeyEvent() { }

	public MessageInfoKeyboardKeyEvent(int vmId, PhysicalKey key, bool keyDown)
	{
		VmId = vmId;
		Key = key;
		KeyDown = keyDown;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Enum.IsDefined(typeof(PhysicalKey), Key);
}

[MessagePackObject]
public class MessageInfoDriveCreated : MessageInfo, IMessageTcp
{
	[Key(1)]
	public DriveGeneralDescriptor Descriptor { get; set; }

	public MessageInfoDriveCreated() { }

	public MessageInfoDriveCreated(DriveGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

[MessagePackObject]
public class MessageInfoDriveConnected : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public int VmId { get; set; }

	public MessageInfoDriveConnected() { }

	public MessageInfoDriveConnected(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoDriveDisconnected : MessageInfo, IMessageTcp
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public int VmId { get; set; }

	public MessageInfoDriveDisconnected() { }

	public MessageInfoDriveDisconnected(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

[MessagePackObject]
public class MessageInfoTransferData : MessageInfo, IMessageTcp
{
	[Key(1)]
	public Guid StreamId { get; set; }
	
	[Key(2)]
	public ulong Offset { get; set; }
	
	[Key(3)]
	public byte[] Data { get; set; }

	public MessageInfoTransferData() { }

	public MessageInfoTransferData(Guid streamId, ulong offset, byte[] data)
	{
		StreamId = streamId;
		Offset = offset;
		Data = data;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && StreamId != Guid.Empty && Data.Length > 0;
}

[MessagePackObject]
public class MessageInfoItemCreated : MessageInfo, IMessageTcp		
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageInfoItemCreated() { }

	public MessageInfoItemCreated(int driveId, string path)
	{
		DriveId = driveId;
		Path = path;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

[MessagePackObject]
public class MessageInfoItemDeleted : MessageInfo, IMessageTcp 
{
	[Key(1)]
	public int DriveId { get; set; }
	
	[Key(2)]
	public string Path { get; set; }

	public MessageInfoItemDeleted() { }

	public MessageInfoItemDeleted(int driveId, string path)
	{
		DriveId = driveId;
		Path = path;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}