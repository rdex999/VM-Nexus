namespace Shared;

public static class SharedDefinitions
{
	public const int ServerPort = 5000;
	public const string ServerIp = "192.168.1.155"; /* local IP in network */

	public const int MessageTimeoutMilliseconds = 3 * 60 * 1000;
	public const int ConnectionDeniedRetryTimeout = 4000;

	public const int CredentialsMaxLength = 64;
	public static readonly char[] InvalidUsernameCharacters = { '"', '\'', '/', '<', '>', '|' };
	
	public enum CpuArchitecture
	{
		X86_64,
		X86,
		Arm,
	}

	public enum DriveType
	{
		Disk,
		CDROM,
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
		FedoraLinux,
		KaliLinux,
		ManjaroLinux,
		MiniCoffeeOS,
		Other,
	}

	public enum VmState
	{
		ShutDown,
		Running,
	}

	public enum BootMode
	{
		Uefi,
		Bios
	}

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

