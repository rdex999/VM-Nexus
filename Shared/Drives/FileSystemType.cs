namespace Shared.Drives;

public enum FileSystemType
{
    Ext4,
    Fat32,
    Ntfs,
    Iso,
    Fat16,
}

public static class FileSystemTypeExtensions
{
    /// <summary>
    /// Get the minimum drive size (in bytes) for the filesystem.
    /// </summary>
    /// <param name="fileSystem">The filesystem type.</param>
    /// <returns>The minimum drive size this filesystem supports, in bytes.</returns>
    /// <remarks>
    /// Precondition: No specific precondition. <br/>
    /// Postcondition: The minimum drive size this filesystem supports is returned, in bytes.
    /// </remarks>
    public static int DriveSizeMin(this FileSystemType fileSystem)
    {
        return fileSystem switch
        {
            FileSystemType.Fat16 => 9 * 1024 * 1024,
            FileSystemType.Fat32 => 512 * 1024 * 1024,
            _ => 10 * 1024 * 1024,
        };
    }
}