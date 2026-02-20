using MessagePack;

namespace Shared.Drives;

[MessagePackObject]
[Union(0, typeof(PathItemDrive))]
[Union(1, typeof(PathItemPartitionGpt))]
[Union(2, typeof(PathItemPartitionMbr))]
[Union(3, typeof(PathItemFile))]
[Union(4, typeof(PathItemDirectory))]
public abstract class PathItem
{
}

[MessagePackObject]
public class PathItemDrive : PathItem
{
	[Key(0)]
	public int Id { get; set; }
	
	[Key(1)]
	public string Name { get; set; }

	public PathItemDrive() { }
		
	public PathItemDrive(int id, string name)
	{
		Id = id;
		Name = name;
	}
}

[MessagePackObject]
public class PathItemPartitionGpt : PathItem
{
	[Key(0)]
	public PartitionGptDescriptor Descriptor { get; set; }
	
	public PathItemPartitionGpt() { }
	
	public PathItemPartitionGpt(PartitionGptDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

[MessagePackObject]
public class PathItemPartitionMbr : PathItem
{
	[Key(0)]
	public PartitionMbrDescriptor Descriptor { get; set; }
	
	public PathItemPartitionMbr() { }
	
	public PathItemPartitionMbr(PartitionMbrDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

[MessagePackObject]
public partial class PathItemFile : PathItem
{
	[Key(0)]
	public string Name { get; set; }
	
	[Key(1)]
	public ulong SizeBytes { get; set; }

	[Key(2)]
	public DateTime Accessed { get; set; }

	[Key(3)]
	public DateTime Modified { get; set; }
	
	[Key(4)]
	public DateTime Created { get; set; }

	public PathItemFile() { }
	
	public PathItemFile(string name, ulong sizeBytes, DateTime accessed, DateTime modified, DateTime created)
	{
		Name = name;
		SizeBytes = sizeBytes;
		Accessed = accessed;
		Modified = modified;
		Created = created;
	}
}

[MessagePackObject]
public class PathItemDirectory : PathItem
{
	[Key(0)]
	public string Name { get; set; }

	[Key(1)]
	public DateTime Modified { get; set; }

	[Key(2)]
	public DateTime Created { get; set; }

	public PathItemDirectory() { }
	
	public PathItemDirectory(string name, DateTime modified, DateTime created)
	{
		Name = name;
		Modified = modified;
		Created = created;
	}
}