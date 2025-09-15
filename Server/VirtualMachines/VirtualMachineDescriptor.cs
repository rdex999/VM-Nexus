using Shared;

namespace Server.VirtualMachines;

public class VirtualMachineDescriptor
{
	public int Id { get; }
	public string Name { get; }
	public SharedDefinitions.OperatingSystem OperatingSystem { get; }
	public SharedDefinitions.CpuArchitecture CpuArchitecture { get; }
	public SharedDefinitions.BootMode BootMode { get; }
	public SharedDefinitions.VmState VmState { get; }

	public VirtualMachineDescriptor(int id, string name, SharedDefinitions.OperatingSystem operatingSystem,
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode,
		SharedDefinitions.VmState vmState)
	{
		Id = id;
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		BootMode = bootMode;
		VmState = vmState;
	}
}