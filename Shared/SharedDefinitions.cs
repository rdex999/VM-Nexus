namespace Shared;

public static class SharedDefinitions
{
	public const int ServerPort = 5000;
	public const string ServerIp = "192.168.1.155";		/* local IP in network */

	public const int MessageTimeoutMilliseconds = 10000;
	public const int ConnectionDeniedRetryTimeout = 4000;

	public const int CredentialsMaxLength = 64;
	
	public enum CpuArchitecture
	{
		X86_64,
		X86,
		Arm,
	}

	public enum DriveType
	{
		NVMe,
		SSD,
		HDD,
		CD_ROM,
		Floppy
	}
	
	public enum OperatingSystem
	{
		Ubuntu,
		KaliLinux,
		ParrotOs,
		ManjaroLinux,
		MiniCoffeeOs,
	}

	public enum VmState
	{
		ShutDown,
		Running,
		Sleeping,
		Hibernated,
	}

	public enum BootMode
	{
		Uefi,
		Bios
	}

	public class VmDescriptor
	{
		public string Name { get; }
		public OperatingSystem OperatingSystem { get; }
		public CpuArchitecture CpuArchitecture { get; }
		public BootMode BootMode { get; }
		public VmState State { get; }

		public VmDescriptor(string name, OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, BootMode bootMode, VmState state)
		{
			Name = name;
			OperatingSystem = operatingSystem;
			CpuArchitecture = cpuArchitecture;
			BootMode = bootMode;
			State = state;
		}
	}
}

