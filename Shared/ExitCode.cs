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
	InvalidMessageData,
	DatabaseStartupFailed,
	DatabaseShutdownFailed,
	DatabaseOperationFailed,
	UserDoesntExist,
	VmAlreadyExists,
	InvalidParameter,
}