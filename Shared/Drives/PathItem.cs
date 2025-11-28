namespace Shared.Drives;

public class PathItem
{
}

public class PathItemDrive : PathItem
{
	public int Id { get; }
	public string Name { get; }

	public PathItemDrive(int id, string name)
	{
		Id = id;
		Name = name;
	}
}

public class PathItemPartitionGpt : PathItem
{
	public PartitionGptDescriptor Descriptor { get; }
	
	public PathItemPartitionGpt(PartitionGptDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

public class PathItemPartitionMbr : PathItem
{
	public PartitionMbrDescriptor Descriptor { get; }
	
	public PathItemPartitionMbr(PartitionMbrDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}

public class PathItemFile : PathItem
{
	public string Name { get; }
	public ulong SizeBytes { get; }
	public DateTime Accessed { get; }
	public DateTime Modified { get; }
	public DateTime Created { get; }

	public PathItemFile(string name, ulong sizeBytes, DateTime accessed, DateTime modified, DateTime created)
	{
		Name = name;
		SizeBytes = sizeBytes;
		Accessed = accessed;
		Modified = modified;
		Created = created;
	}
}

public class PathItemDirectory : PathItem
{
	public string Name { get; }
	public DateTime Modified { get; }
	public DateTime Created { get; }

	public PathItemDirectory(string name, DateTime modified, DateTime created)
	{
		Name = name;
		Modified = modified;
		Created = created;
	}
}