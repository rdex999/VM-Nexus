namespace Shared.Drives;

public class DriveGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; }
    public int Size { get; }			/* Disk image size, in MiB */
    public DriveType DriveType { get; }
		
    public DriveGeneralDescriptor(int id, string name, int size, DriveType driveType)
    {
        Id = id;
        Name = name;
        Size = size;
        DriveType = driveType;
    }
}