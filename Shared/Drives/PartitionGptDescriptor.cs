namespace Shared.Drives;

public class PartitionGptDescriptor
{
	public Type TypeGuid { get; }
	public Guid Guid { get; }
	public long StartLba { get; }
	public long EndLba { get; }
	public Attribute Flags { get; }
	public string Label { get; }

	public PartitionGptDescriptor(Type typeGuid, Guid guid, long startLba, long endLba, Attribute flags, string label)
	{
		TypeGuid = typeGuid;
		Guid = guid;
		StartLba = startLba;
		EndLba = endLba;
		Flags = flags;
		Label = label;
	}
	
	public enum Type
	{
		Unknown,
		EfiSystem,
		LinuxFilesystemData,
		LinuxSwap,
	}
	
	public enum Attribute
	{
		PlatformRequired	= 1 << 0,
		Ignore				= 1 << 1,
		LegacyBiosBootable	= 1 << 2,
	}
}
