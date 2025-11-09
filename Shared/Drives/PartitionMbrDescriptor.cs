namespace Shared.Drives;

public class PartitionMbrDescriptor
{
	public bool Bootable { get; }
	public Type PartitionType { get; }
	public long StartLba { get; }
	public long Sectors { get; }

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