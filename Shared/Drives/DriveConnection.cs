using MessagePack;

namespace Shared.Drives;

[MessagePackObject]
public struct DriveConnection
{
    [Key(0)]
    public int DriveId { get; set; }
    
    [Key(1)]
    public int VmId { get; set; }
		
    public DriveConnection(int driveId, int vmId)
    {
        DriveId = driveId;
        VmId = vmId;
    }
}