using Shared;

namespace Server.VirtualMachines;

public class VirtualMachineDescriptor
{
	public string Name;
	public SharedDefinitions.OperatingSystem OperatingSystem;
	public SharedDefinitions.CpuArchitecture CpuArchitecture;
	public SharedDefinitions.BootMode BootMode;
	public SharedDefinitions.VmState VmState;

	public VirtualMachineDescriptor(string name, SharedDefinitions.OperatingSystem operatingSystem,
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode,
		SharedDefinitions.VmState vmState)
	{
		Name = name;
		OperatingSystem = operatingSystem;
		CpuArchitecture = cpuArchitecture;
		BootMode = bootMode;
		VmState = vmState;
	}
}