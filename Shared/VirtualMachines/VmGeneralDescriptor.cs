using MessagePack;

namespace Shared.VirtualMachines;

[MessagePackObject]
public class VmGeneralDescriptor
{
    [Key(0)]
    public int Id { get; set; }
    
    [Key(1)]
    public string Name { get; set; }
    
    [Key(2)]
    public OperatingSystem OperatingSystem { get; set; }
    
    [Key(3)]
    public CpuArchitecture CpuArchitecture { get; set; }
    
    [Key(4)]
    public VmState State { get; set; }
    
    [Key(5)]
    public int RamSizeMiB { get; set; }
    
    [Key(6)]
    public BootMode BootMode { get; set; }

    public VmGeneralDescriptor() { }
    
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