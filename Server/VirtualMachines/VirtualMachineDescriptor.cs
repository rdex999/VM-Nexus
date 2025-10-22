using Shared;
using Shared.VirtualMachines;

namespace Server.VirtualMachines;

public class VirtualMachineDescriptor
{
	public int Id { get; }
	public string Name { get; }
	public OperatingSystem OperatingSystem { get; }
	public CpuArchitecture CpuArchitecture { get; }
	public SharedDefinitions.BootMode BootMode { get; }
	public VmState VmState { get; }

	public VirtualMachineDescriptor(int id, string name, OperatingSystem operatingSystem,
		CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode,
		VmState vmState)
	{
		Id = id;
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		BootMode = bootMode;
		VmState = vmState;
	}
}