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

	public enum FilesystemType
	{
		Ext4,
		Fat16,
	}

	public enum PartitionTableType
	{
		Mbr,
		Gpt,
	}
	
	public enum OperatingSystem
	{
		Ubuntu,
		KaliLinux,
		ParrotOS,
		ManjaroLinux,
		MiniCoffeeOS,
		Other,
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

	public class VmGeneralDescriptor
	{
		public string Name { get; }
		public OperatingSystem OperatingSystem { get; }
		public VmState State { get; }

		public VmGeneralDescriptor(string name, OperatingSystem operatingSystem, VmState state)
		{
			Name = name;
			OperatingSystem = operatingSystem;
			State = state;
		}
	}

	public class PartitionDescriptor
	{
		public string Lable { get; }
		public FilesystemType FilesystemType { get; }
		public int Size { get; }	/* The partitions size in MiB. */
		public bool Bootable { get; }

		public PartitionDescriptor(string lable, FilesystemType filesystemType, int size, bool bootable)
		{
			Lable = lable;
			FilesystemType = filesystemType;
			Size = size;
			Bootable = bootable;
		}
	}
}

