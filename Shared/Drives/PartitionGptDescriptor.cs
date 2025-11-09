namespace Shared.Drives;

public class PartitionGptDescriptor
{
	public Guid Guid { get; }
	public long StartLba { get; }
	public long EndLba { get; }
	public Attribute Flags { get; }
	public string Label { get; }
	public string Type { get; }

	public PartitionGptDescriptor(Guid guid, long startLba, long endLba, Attribute flags, string label, string type)
	{
		Guid = guid;
		StartLba = startLba;
		EndLba = endLba;
		Flags = flags;
		Label = label;
		Type = type;
	}
	
	public enum Attribute : long
	{
		PlatformRequired	= 1 << 0,
		Ignore				= 1 << 1,
		LegacyBiosBootable	= 1 << 2,
	}
}
