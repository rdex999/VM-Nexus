namespace Shared.VirtualMachines;

public class VmGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; set; }
    public OperatingSystem OperatingSystem { get; set; }
    public VmState State { get; set; }

    public VmGeneralDescriptor(int id, string name, OperatingSystem operatingSystem, VmState state)
    {
        Id = id;
        Name = name;
        OperatingSystem = operatingSystem;
        State = state;
    }
}