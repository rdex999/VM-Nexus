using MessagePack;

namespace Shared.Drives;

[MessagePackObject]
public class DriveGeneralDescriptor
{
    [Key(0)]
    public int Id { get; }
    
    [Key(1)]
    public string Name { get; }
    
    [Key(2)]
    public int Size { get; }			/* Disk image size, in MiB */
    
    [Key(3)]
    public int SectorSize { get; }      /* Size of each sector, in bytes. */
    
    [Key(4)]
    public DriveType DriveType { get; }
    
    [Key(5)]
    public PartitionTableType PartitionTableType { get; }
		
    public DriveGeneralDescriptor() { }
    
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