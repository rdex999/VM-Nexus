using Shared.Drives;

namespace Shared;

public static class SharedDefinitions
{
	public const int ServerTcpPort = 5000;
	public const int ServerUdpPort = 5001;
	public const string ServerIp = "172.20.10.2"; /* local IP in network */

	public const int MessageTimeoutMilliseconds = 3 * 60 * 1000;
	public const int ConnectionDeniedRetryTimeout = 4000;

	public const int CredentialsMaxLength = 64;
	public static readonly char[] InvalidUsernameCharacters = { '"', '\'', '/', '<', '>', '|' };

	public const int AudioFramesFrequency = 48000;
	public const int AudioPacketMs = 20;
	public const int AudioChannels = 2;
	public const int AudioFramesPerPacket = (int)((float)AudioFramesFrequency * ((float)AudioPacketMs / 1000.0));
	public const int AudioBytesPerPacket = AudioFramesPerPacket * AudioChannels * 2;	/* Using two channels, s16le */

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

	public enum MouseButtons
	{
		None		= 0,
		Left		= 1 << 0,
		Middle		= 1 << 1,
		Right		= 1 << 2,
		WheelUp		= 1 << 3,
		WheelDown	= 1 << 4,
		WheelLeft	= 1 << 5,
		WheelRight	= 1 << 6,
	}
}

