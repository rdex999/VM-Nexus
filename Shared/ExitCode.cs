namespace Shared;

public enum ExitCode
{
	Success = 0,
	ServerNoValidLocalhostIp,
	ConnectionToServerFailed,
	DisconnectedFromServer,
	MessageSendingTimeout,
	MessageReceivingTimeout,
	MessageReceivedCorrupted,
}