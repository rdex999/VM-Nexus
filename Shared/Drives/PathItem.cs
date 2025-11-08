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