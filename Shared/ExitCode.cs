namespace Shared;

public enum ExitCode
{
	Success = 0,
	InvalidParameter,
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
	VmDoesntExist,
	DriveAlreadyExists,
	DriveDoesntExist,
	DriveConnectionAlreadyExists,
	DiskImageCreationFailed,
}