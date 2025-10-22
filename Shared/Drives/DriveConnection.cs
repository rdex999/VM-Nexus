namespace Shared.Drives;

public struct DriveConnection
{
    public int DriveId { get; }
    public int VmId { get; }
		
    public DriveConnection(int driveId, int vmId)
    {
        DriveId = driveId;
        VmId = vmId;
    }
}