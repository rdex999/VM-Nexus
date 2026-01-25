namespace Shared.VirtualMachines;

public class VmGeneralDescriptor
{
    public int Id { get; }
    public string Name { get; set; }
    public OperatingSystem OperatingSystem { get; set; }
    public CpuArchitecture CpuArchitecture { get; set; }
    public VmState State { get; set; }
    public int RamSizeMiB { get; }
    public BootMode BootMode { get; set; }

    public VmGeneralDescriptor(int id, string name, OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, VmState state, int ramSizeMiB, BootMode bootMode)
    {
        Id = id;
        Name = name;
        OperatingSystem = operatingSystem;
        CpuArchitecture = cpuArchitecture;
        State = state;
        RamSizeMiB = ramSizeMiB;
        BootMode = bootMode;
    }
}