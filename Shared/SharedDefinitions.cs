
namespace Shared;

public static class SharedDefinitions
{
	public const int ServerTcpPort = 5000;
	public const int ServerTcpWebPort = 5001;
	public const int ServerUdpPort = 5002;
	public const string ServerIp = "192.168.1.155";		/* local IP in network */

	public const int MaxUdpMessageSize = 150 * 1024 * 1024;
	
	public const int MessageTimeoutMilliseconds = 3 * 60 * 1000;
	public const int ConnectionDeniedRetryTimeout = 4000;

	public const int CredentialsMaxLength = 64;
	public const int PasswordMinLength = 8;
	public const int BadLoginBlockCount = 7;
	public const int BadLoginBlockMinutes = 5;
	public static readonly char[] InvalidUsernameCharacters = { '"', '\'', '/', '<', '>', '|' };
	public static readonly char[] DirectorySeparators = { '/', '\\' };
	
	public const int AudioFramesFrequency = 22050;
	public const int AudioPacketMs = 50;
	public const int AudioChannels = 2;
	public const int AudioFramesPerPacket = (int)((float)AudioFramesFrequency * ((float)AudioPacketMs / 1000.0));
	public const int AudioBytesPerPacket = AudioFramesPerPacket * AudioChannels * 2;	/* Using two channels, s16le */

	public const int VmRamSizeMbMax = 1024 * 8;
	public const int DriveSizeMbMax = 256 * 1024;
}

