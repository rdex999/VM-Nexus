namespace Shared.VirtualMachines;

public class VmGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; set; }
    public OperatingSystem OperatingSystem { get; set; }
    public VmState State { get; set; }
    public int RamSizeMiB { get; }

    public VmGeneralDescriptor(int id, string name, OperatingSystem operatingSystem, VmState state, int ramSizeMiB)
    {
        Id = id;
        Name = name;
        OperatingSystem = operatingSystem;
        State = state;
        RamSizeMiB = ramSizeMiB;
    }
}