using System.Drawing;
using System.Net;
using Avalonia.Input;
using Newtonsoft.Json;
using Shared.Drives;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Shared.Networking;

public interface IMessageInfo : IMessage {}

public abstract class MessageInfo : Message, IMessageInfo
{
	public MessageInfo() {}

	[JsonConstructor]
	protected MessageInfo(Guid id) : base(id) {}
}

public class MessageInfoIdentifyUdpPort : MessageInfo, IMessageTcp
{
	public int Port { get; }

	public MessageInfoIdentifyUdpPort(int port)
	{
		Port = port;
	}
	
	[JsonConstructor]
	private MessageInfoIdentifyUdpPort(Guid id, int port)
		: base(id)
	{
		Port = port;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && Port >= IPEndPoint.MinPort && Port <= IPEndPoint.MaxPort;
}

public class MessageInfoCryptoUdp : MessageInfo, IMessageTcp
{
	public byte[] MasterKey32 { get; }
	public byte[] Salt32 { get; }
	
	public MessageInfoCryptoUdp(byte[] masterKey32, byte[] salt32)
	{
		MasterKey32 = masterKey32;
		Salt32 = salt32;
	}
	
	[JsonConstructor]
	private MessageInfoCryptoUdp(Guid id, byte[] masterKey32, byte[] salt32)
		: base(id)
	{
		MasterKey32 = masterKey32;
		Salt32 = salt32;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && MasterKey32.Length == 32 && Salt32.Length == 32;
}

public class MessageInfoSubUserCreated : MessageInfo, IMessageTcp
{
	public SubUser SubUser { get; }

	public MessageInfoSubUserCreated(SubUser subUser)
	{
		SubUser = subUser;
	}
	
	[JsonConstructor]
	private MessageInfoSubUserCreated(Guid id, SubUser subUser)
		: base(id)
	{
		SubUser = subUser;
	}
}

public class MessageInfoUserData : MessageInfo, IMessageTcp
{
	public User User { get; }

	public MessageInfoUserData(User user)
	{
		User = user;
	}

	[JsonConstructor]
	public MessageInfoUserData(Guid id, User user)
		: base(id)
	{
		User = user;
	}
}

public class MessageInfoUserDeleted : MessageInfo, IMessageTcp
{
	public int UserId { get; }

	public MessageInfoUserDeleted(int userId)
	{
		UserId = userId;
	}
	
	[JsonConstructor]
	private MessageInfoUserDeleted(Guid id, int userId)
		: base(id)
	{
		UserId = userId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && UserId >= 1;
}

public class MessageInfoVmCreated : MessageInfo, IMessageTcp
{
	public VmGeneralDescriptor Descriptor { get; }

	public MessageInfoVmCreated(VmGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
	
	[JsonConstructor]
	private MessageInfoVmCreated(Guid id, VmGeneralDescriptor descriptor)
		: base(id)
	{
		Descriptor = descriptor;
	}

	public override bool IsValidMessage() => base.IsValidMessage() 
	                                         && !string.IsNullOrEmpty(Descriptor.Name) && Descriptor.Id >= 1
	                                         && Enum.IsDefined(typeof(OperatingSystem), Descriptor.OperatingSystem)
	                                         && Enum.IsDefined(typeof(VmState), Descriptor.State);
}

public class MessageInfoVmDeleted : MessageInfo, IMessageTcp 
{
	public int VmId { get; }

	public MessageInfoVmDeleted(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoVmDeleted(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmScreenFrame : MessageInfo, IMessageUdp
{
	public int VmId { get; }
	public Size Size { get; }
	public byte[] CompressedFramebuffer { get; }

	public MessageInfoVmScreenFrame(int vmId, Size size, byte[] compressedFramebuffer)
	{
		VmId = vmId;
		Size = size;
		CompressedFramebuffer = compressedFramebuffer;
	}

	[JsonConstructor]
	private MessageInfoVmScreenFrame(Guid id, int vmId, Size size, byte[] compressedFramebuffer)
		: base(id)
	{
		VmId = vmId;
		Size = size;
		CompressedFramebuffer = compressedFramebuffer;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 &&
	                                         Size.Width > 0 && Size.Height > 0 && 
	                                         CompressedFramebuffer.Length > 0;
}

public class MessageInfoVmAudioPacket : MessageInfo, IMessageUdp
{
	public int VmId { get; }
	public byte[] Packet { get; }

	public MessageInfoVmAudioPacket(int vmId, byte[] packet)
	{
		VmId = vmId;
		Packet = packet;
	}
	
	[JsonConstructor]
	private MessageInfoVmAudioPacket(Guid id, int vmId, byte[] packet)
		: base(id)
	{
		VmId = vmId;
		Packet = packet;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Packet != null && Packet.Length > 0;
}

public class MessageInfoVmPoweredOn : MessageInfo, IMessageTcp
{
	public int VmId { get; }

	public MessageInfoVmPoweredOn(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoVmPoweredOn(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmPoweredOff : MessageInfo, IMessageTcp
{
	public int VmId { get; }

	public MessageInfoVmPoweredOff(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoVmPoweredOff(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoVmCrashed : MessageInfo, IMessageTcp 
{
	public int VmId { get; }

	public MessageInfoVmCrashed(int vmId)
	{
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoVmCrashed(Guid id, int vmId)
		: base(id)
	{
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1;
}

public class MessageInfoPointerMoved : MessageInfo, IMessageTcp
{
	public int VmId { get; }
	public Point Position { get; }

	public MessageInfoPointerMoved(int vmId, Point position)
	{
		VmId = vmId;
		Position = position;
	}
	
	[JsonConstructor]
	private MessageInfoPointerMoved(Guid id, int vmId, Point position)
		: base(id)
	{
		VmId = vmId;
		Position = position;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Position.X >= 0 && Position.Y >= 0;
}

public class MessageInfoPointerButtonEvent : MessageInfo, IMessageTcp 
{
	public int VmId { get; }
	public Point Position { get; }
	public int PressedButtons { get; }		/* Flags - from MouseButtons. */

	public MessageInfoPointerButtonEvent(int vmId, Point position, int pressedButtons)
	{
		VmId = vmId;
		Position = position;
		PressedButtons = pressedButtons;
	}
	
	[JsonConstructor]
	private MessageInfoPointerButtonEvent(Guid id, int vmId, Point position, int pressedButtons)
		: base(id)
	{
		VmId = vmId;
		Position = position;
		PressedButtons = pressedButtons;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Position.X >= 0 && Position.Y >= 0;
}

public class MessageInfoKeyboardKeyEvent : MessageInfo, IMessageTcp
{
	public int VmId { get; }
	public PhysicalKey Key { get; }
	public bool KeyDown { get; }

	public MessageInfoKeyboardKeyEvent(int vmId, PhysicalKey key, bool keyDown)
	{
		VmId = vmId;
		Key = key;
		KeyDown = keyDown;
	}
	
	[JsonConstructor]
	private MessageInfoKeyboardKeyEvent(Guid id, int vmId, PhysicalKey key, bool keyDown)
		: base(id)
	{
		VmId = vmId;
		Key = key;
		KeyDown = keyDown;
	}

	
	public override bool IsValidMessage() => base.IsValidMessage() && VmId >= 1 && Enum.IsDefined(typeof(PhysicalKey), Key);
}

public class MessageInfoDriveCreated : MessageInfo, IMessageTcp
{
	public DriveGeneralDescriptor Descriptor { get; }

	public MessageInfoDriveCreated(DriveGeneralDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
	
	[JsonConstructor]
	private MessageInfoDriveCreated(Guid id, DriveGeneralDescriptor descriptor)
		: base(id)
	{
		Descriptor = descriptor;
	}
}

public class MessageInfoDriveConnected : MessageInfo, IMessageTcp
{
	public int DriveId { get; }
	public int VmId { get; }

	public MessageInfoDriveConnected(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoDriveConnected(Guid id, int driveId, int vmId)
		: base(id)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

public class MessageInfoDriveDisconnected : MessageInfo, IMessageTcp
{
	public int DriveId { get; }
	public int VmId { get; }

	public MessageInfoDriveDisconnected(int driveId, int vmId)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	[JsonConstructor]
	private MessageInfoDriveDisconnected(Guid id, int driveId, int vmId)
		: base(id)
	{
		DriveId = driveId;
		VmId = vmId;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1 && VmId >= 1;
}

public class MessageInfoTransferData : MessageInfo, IMessageTcp
{
	public Guid StreamId { get; }
	public ulong Offset { get; }		/* The offset of the data in the file, in bytes. */
	public byte[] Data { get; }

	public MessageInfoTransferData(Guid streamId, ulong offset, byte[] data)
	{
		StreamId = streamId;
		Offset = offset;
		Data = data;
	}

	[JsonConstructor]
	private MessageInfoTransferData(Guid id, Guid streamId, ulong offset, byte[] data)
		: base(id)
	{
		StreamId = streamId;
		Offset = offset;
		Data = data;
	}
	public override bool IsValidMessage() => base.IsValidMessage() && StreamId != Guid.Empty && Data.Length > 0;
}

/* "Item" In this case does NOT include drives, as there is MessageInfoDriveCreated. */
public class MessageInfoItemCreated : MessageInfo, IMessageTcp		
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageInfoItemCreated(int driveId, string path)
	{
		DriveId = driveId;
		Path = path;
	}
	
	[JsonConstructor]
	private MessageInfoItemCreated(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = path;
	}
	
	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}

/* "Item" In this case includes a drive. */
public class MessageInfoItemDeleted : MessageInfo, IMessageTcp 
{
	public int DriveId { get; }
	public string Path { get; }

	public MessageInfoItemDeleted(int driveId, string path)
	{
		DriveId = driveId;
		Path = path;
	}
	
	[JsonConstructor]
	private MessageInfoItemDeleted(Guid id, int driveId, string path)
		: base(id)
	{
		DriveId = driveId;
		Path = path;
	}

	public override bool IsValidMessage() => base.IsValidMessage() && DriveId >= 1;
}