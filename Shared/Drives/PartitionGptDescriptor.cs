using MessagePack;

namespace Shared.Drives;

[MessagePackObject]
public class PartitionGptDescriptor
{
	[Key(0)]
	public Guid Guid { get; set; }
	
	[Key(1)]
	public long StartLba { get; set; }
	
	[Key(2)]
	public long EndLba { get; set; }
	
	[Key(3)]
	public Attribute Flags { get; set; }
	
	[Key(4)]
	public string Label { get; set; }
	
	[Key(5)]
	public string Type { get; set; }

	public PartitionGptDescriptor() { }
	
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
