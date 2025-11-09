
namespace Shared;

public static class SharedDefinitions
{
	public const int ServerTcpPort = 5000;
	public const int ServerUdpPort = 5001;
	public const string ServerIp = "192.168.1.155";		/* local IP in network */

	public const int MessageTimeoutMilliseconds = 3 * 60 * 1000;
	public const int ConnectionDeniedRetryTimeout = 4000;

	public const int CredentialsMaxLength = 64;
	public static readonly char[] InvalidUsernameCharacters = { '"', '\'', '/', '<', '>', '|' };
	public static readonly char[] DirectorySeparators = { '/', '\\' };
	
	public const int AudioFramesFrequency = 48000;
	public const int AudioPacketMs = 20;
	public const int AudioChannels = 2;
	public const int AudioFramesPerPacket = (int)((float)AudioFramesFrequency * ((float)AudioPacketMs / 1000.0));
	public const int AudioBytesPerPacket = AudioFramesPerPacket * AudioChannels * 2;	/* Using two channels, s16le */
}

