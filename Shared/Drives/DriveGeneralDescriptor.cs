namespace Shared.Drives;

public class DriveGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; }
    public int Size { get; }			/* Disk image size, in MiB */
    public int SectorSize { get; }      /* Size of each sector, in bytes. */
    public DriveType DriveType { get; }
    public PartitionTableType PartitionTableType { get; }
		
    public DriveGeneralDescriptor(int id, string name, int size, int sectorSize, DriveType driveType, PartitionTableType partitionTableType)
    {
        Id = id;
        Name = name;
        Size = size;
        SectorSize = sectorSize;
        DriveType = driveType;
        PartitionTableType = partitionTableType;
    }
}