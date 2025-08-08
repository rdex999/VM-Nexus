namespace Shared;

public static class SharedDefinitions
{
	public const int ServerPort = 5000;
	public const string ServerIp = "192.168.1.155";		/* local IP in network */

	public const int MessageTimeoutMilliseconds = 1000 * 60 * 2; //3000;
	public const int ConnectionDeniedRetryTimeout = 1000 * 60 * 2; // 8000;
}

