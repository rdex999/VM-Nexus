using MessagePack;

namespace Shared.Networking;

public interface IMessage
{
	public bool IsValidMessage();
}

public interface IMessageTcp : IMessage {}

public interface IMessageUdp : IMessage
{
	public Guid Id { get; set; }
}

[MessagePackObject]
[Union(0, typeof(MessageInfoIdentifyUdpPort))]
[Union(1, typeof(MessageInfoCryptoUdp))]
[Union(2, typeof(MessageInfoSubUserCreated))]
[Union(3, typeof(MessageInfoUserData))]
[Union(4, typeof(MessageInfoUserDeleted))]
[Union(5, typeof(MessageInfoVmCreated))]
[Union(6, typeof(MessageInfoVmDeleted))]
[Union(7, typeof(MessageInfoVmScreenFrame))]
[Union(8, typeof(MessageInfoVmAudioPacket))]
[Union(9, typeof(MessageInfoVmPoweredOn))]
[Union(10, typeof(MessageInfoVmPoweredOff))]
[Union(11, typeof(MessageInfoVmCrashed))]
[Union(12, typeof(MessageInfoPointerMoved))]
[Union(13, typeof(MessageInfoPointerButtonEvent))]
[Union(14, typeof(MessageInfoKeyboardKeyEvent))]
[Union(15, typeof(MessageInfoDriveCreated))]
[Union(16, typeof(MessageInfoDriveConnected))]
[Union(17, typeof(MessageInfoDriveDisconnected))]
[Union(18, typeof(MessageInfoTransferData))]
[Union(19, typeof(MessageInfoItemCreated))]
[Union(20, typeof(MessageInfoItemDeleted))]

[Union(101, typeof(MessageRequestCheckUsername))]
[Union(102, typeof(MessageRequestCreateAccount))]
[Union(103, typeof(MessageRequestDeleteAccount))]
[Union(104, typeof(MessageRequestLogin))]
[Union(105, typeof(MessageRequestLogout))]
[Union(106, typeof(MessageRequestLoginSubUser))]
[Union(107, typeof(MessageRequestCreateSubUser))]
[Union(108, typeof(MessageRequestSetOwnerPermissions))]
[Union(109, typeof(MessageRequestResetPassword))]
[Union(110, typeof(MessageRequestListSubUsers))]
[Union(111, typeof(MessageRequestCreateVm))]
[Union(112, typeof(MessageRequestDeleteVm))]
[Union(113, typeof(MessageRequestListVms))]
[Union(114, typeof(MessageRequestCheckVmExist))]
[Union(115, typeof(MessageRequestCreateDriveFs))]
[Union(116, typeof(MessageRequestCreateDriveFromImage))]
[Union(117, typeof(MessageRequestCreateDriveOs))]
[Union(118, typeof(MessageRequestConnectDrive))]
[Union(119, typeof(MessageRequestDisconnectDrive))]
[Union(120, typeof(MessageRequestListDriveConnections))]
[Union(121, typeof(MessageRequestListDrives))]
[Union(122, typeof(MessageRequestListPathItems))]
[Union(123, typeof(MessageRequestDownloadItem))]
[Union(124, typeof(MessageRequestUploadFile))]
[Union(125, typeof(MessageRequestCreateDirectory))]
[Union(126, typeof(MessageRequestDeleteItem))]
[Union(127, typeof(MessageRequestVmStartup))]
[Union(128, typeof(MessageRequestVmShutdown))]
[Union(129, typeof(MessageRequestVmForceOff))]
[Union(130, typeof(MessageRequestVmStreamStart))]
[Union(131, typeof(MessageRequestVmStreamStop))]

[Union(201, typeof(MessageResponseInvalidRequestData))]
[Union(202, typeof(MessageResponseCheckUsername))]
[Union(203, typeof(MessageResponseCreateAccount))]
[Union(204, typeof(MessageResponseDeleteAccount))]
[Union(205, typeof(MessageResponseLogin))]
[Union(206, typeof(MessageResponseLogout))]
[Union(207, typeof(MessageResponseLoginSubUser))]
[Union(208, typeof(MessageResponseCreateSubUser))]
[Union(209, typeof(MessageResponseSetOwnerPermissions))]
[Union(210, typeof(MessageResponseResetPassword))]
[Union(211, typeof(MessageResponseListSubUsers))]
[Union(212, typeof(MessageResponseCreateVm))]
[Union(213, typeof(MessageResponseDeleteVm))]
[Union(214, typeof(MessageResponseListVms))]
[Union(215, typeof(MessageResponseCheckVmExist))]
[Union(216, typeof(MessageResponseCreateDriveFs))]
[Union(217, typeof(MessageResponseCreateDriveFromImage))]
[Union(218, typeof(MessageResponseCreateDriveOs))]
[Union(219, typeof(MessageResponseConnectDrive))]
[Union(220, typeof(MessageResponseDisconnectDrive))]
[Union(221, typeof(MessageResponseListDriveConnections))]
[Union(222, typeof(MessageResponseListDrives))]
[Union(223, typeof(MessageResponseListPathItems))]
[Union(224, typeof(MessageResponseDownloadItem))]
[Union(225, typeof(MessageResponseUploadFile))]
[Union(226, typeof(MessageResponseCreateDirectory))]
[Union(227, typeof(MessageResponseDeleteItem))]
[Union(228, typeof(MessageResponseVmStartup))]
[Union(229, typeof(MessageResponseVmShutdown))]
[Union(230, typeof(MessageResponseVmForceOff))]
[Union(231, typeof(MessageResponseVmStreamStart))]
[Union(232, typeof(MessageResponseVmStreamStop))]
public abstract class Message : IMessage
{
	public virtual bool IsValidMessage() => true;
}