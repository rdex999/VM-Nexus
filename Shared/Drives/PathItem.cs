namespace Shared.Drives;

public class PathItem
{
}

public class PathItemPartitionGpt : PathItem
{
	public PartitionGptDescriptor Descriptor { get; }
	
	public PathItemPartitionGpt(PartitionGptDescriptor descriptor)
	{
		Descriptor = descriptor;
	}
}