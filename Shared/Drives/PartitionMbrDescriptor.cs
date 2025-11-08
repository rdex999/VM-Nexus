namespace Shared.Drives;

public class PartitionMbrDescriptor
{
	public bool Bootable { get; }
	public Type PartitionType { get; }
	public int StartLba { get; }
	public int Sectors { get; }
	
	public enum Type
	{
		NtfsOrExFat		= 0x07,
		Linux			= 0x83,
		Extended1		= 0x05,		/* Either one makes the partition type extended */
		Extended2		= 0x0F,
	}
}