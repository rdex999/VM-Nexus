using MessagePack;

namespace Shared.Drives;

[MessagePackObject]
public class PartitionMbrDescriptor
{
	[Key(0)]
	public bool Bootable { get; set; }
	
	[Key(1)]
	public Type PartitionType { get; set; }
	
	[Key(2)]
	public long StartLba { get; set; }
	
	[Key(3)]
	public long Sectors { get; set; }
	
	public PartitionMbrDescriptor(bool bootable, Type partitionType, long startLba, long sectors)
	{
		Bootable = bootable;
		PartitionType = partitionType;
		StartLba = startLba;
		Sectors = sectors;
	}
	
	public enum Type : byte
	{
		NtfsOrExFat		= 0x07,
		Linux			= 0x83,
		Extended1		= 0x05,		/* Either one makes the partition type extended */
		Extended2		= 0x0F,
	}
}