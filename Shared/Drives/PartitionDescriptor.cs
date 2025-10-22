namespace Shared.Drives;

public class PartitionDescriptor
{
    public string Lable { get; }
    public FilesystemType FilesystemType { get; }
    public int Size { get; }	/* The partitions size in MiB. */
    public bool Bootable { get; }

    public PartitionDescriptor(string lable, FilesystemType filesystemType, int size, bool bootable)
    {
        Lable = lable;
        FilesystemType = filesystemType;
        Size = size;
        Bootable = bootable;
    }
}