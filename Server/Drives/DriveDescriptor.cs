using Shared;

namespace Server.Drives;

public class DriveDescriptor
{
	public int Id { get; }
	public string Name { get; }
	public int Size { get; }	/* The size of the drive in MiB */
	public SharedDefinitions.DriveType Type { get; }
	
	public DriveDescriptor(int id, string name, int size, SharedDefinitions.DriveType type)
	{
		Id = id;
		Name = name;
		Size = size;
		Type = type;
	}
}