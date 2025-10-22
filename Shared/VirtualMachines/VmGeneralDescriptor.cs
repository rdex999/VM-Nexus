namespace Shared.VirtualMachines;

public class VmGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; set; }
    public SharedDefinitions.OperatingSystem OperatingSystem { get; set; }
    public SharedDefinitions.VmState State { get; set; }

    public VmGeneralDescriptor(int id, string name, SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.VmState state)
    {
        Id = id;
        Name = name;
        OperatingSystem = operatingSystem;
        State = state;
    }
}