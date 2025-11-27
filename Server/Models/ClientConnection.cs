using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Drives;
using Server.Services;
using Server.VirtualMachines;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Server.Models;

public sealed class ClientConnection : MessagingService
{
	public event EventHandler? Disconnected;
	public int UserId { get; private set; } = -1;
	public Guid ClientId { get; private init; }
	private bool _isLoggedIn = false;
	private readonly DatabaseService _databaseService;
	private readonly UserService _userService;
	private readonly VirtualMachineService _virtualMachineService;
	private readonly DriveService _driveService;
	private bool _hasDisconnected = false;		/* Has the Disconnect function run? */

	private int _streamVmId = -1;

	/// <summary>
	/// Creates and initializes the ClientConnection object.
	/// </summary>
	/// <param name="tcpSocket">The socket on which the client has connected. socket != null.</param>
	/// <param name="databaseService">A reference to the database service. databaseService != null.</param>
	/// <param name="userService">A reference to the user service. userService != null.</param>
	/// <param name="virtualMachineService">A reference to the virtual machine service. virtualMachineService != null.</param>
	/// <param name="driveService">A reference to the drive service. driveService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server.
	/// socket != null &amp;&amp; userService != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null. &amp;&amp; driveService != null.<br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(Socket tcpSocket, DatabaseService databaseService, UserService userService, VirtualMachineService virtualMachineService, DriveService driveService)
	{
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		ClientId = Guid.NewGuid();

		Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		udpSocket.Bind(new IPEndPoint(IPAddress.Any, SharedDefinitions.ServerUdpPort));

		TcpSocket = tcpSocket;
		UdpSocket = udpSocket;
		
		IsServiceInitialized = true;
	
		/* UDP will be started once a MessageInfoIdentifyUdp is received (using TCP socket) from the client. */
		StartTcp();
	}

	/// <summary>
	/// Handles requests from the client.
	/// </summary>
	/// <param name="request">
	/// The request that was sent by the client. request != null.
	/// </param>
	/// <remarks>
	/// Precondition: A request has been sent by the client to the server. <br/>
	/// Postcondition: Request is handled and a response has been sent.
	/// </remarks>
	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result = ExitCode.Success;
		switch (request)
		{
			case MessageRequestCheckUsername reqCheckUsername:
			{
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(reqCheckUsername.Username.Trim());
				SendResponse(new MessageResponseCheckUsername(true, reqCheckUsername.Id, usernameAvailable));
				break;
			}

			case MessageRequestCreateAccount reqCreateAccount:
			{
				if (!Common.IsValidUsername(reqCreateAccount.Username))
				{
					SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, MessageResponseCreateAccount.Status.InvalidUsernameSyntax));
					break;
				}

				if (!Common.IsValidEmail(reqCreateAccount.Email))
				{
					SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, MessageResponseCreateAccount.Status.InvalidEmail));
					break;
				}
				
				string usernameTrimmed = reqCreateAccount.Username.Trim();
				string emailTrimmed = reqCreateAccount.Email.Trim();
				
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(usernameTrimmed);
				MessageResponseCreateAccount.Status status;
				if (usernameAvailable)
				{
					ExitCode code = await _databaseService.RegisterUserAsync(usernameTrimmed, emailTrimmed, reqCreateAccount.Password);
					if (code == ExitCode.Success)
					{
						status = MessageResponseCreateAccount.Status.Success;
						_isLoggedIn = true;
						UserId = await _databaseService.GetUserIdAsync(usernameTrimmed);		/* Must be valid because created user successfully. */
					}
					else
					{
						status = MessageResponseCreateAccount.Status.Failure;
					}
				}
				else
				{
					status = MessageResponseCreateAccount.Status.UsernameNotAvailable;
				}
				
				SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, status));
				break;
			}

			case MessageRequestLogin reqLogin:
			{
				string usernameTrimmed = reqLogin.Username.Trim();
				result = await _userService.LoginAsync(usernameTrimmed, reqLogin.Password, this);
				if (result == ExitCode.Success)
				{
					_isLoggedIn = true;
					UserId = await _databaseService.GetUserIdAsync(usernameTrimmed);	/* Must be valid because the user exists. (IsValidLogin would return false otherwise) */

					VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(UserId);
					if (vms == null)
					{
						break;
					}

					foreach (VmGeneralDescriptor vm in vms)
					{
						if (vm.State == VmState.Running)
						{
							_virtualMachineService.SubscribeToVmPoweredOff(vm.Id, OnVirtualMachinePoweredOffOrCrashed);
							_virtualMachineService.SubscribeToVmCrashed(vm.Id, OnVirtualMachinePoweredOffOrCrashed);
						}
					}
				}
				
				SendResponse(new MessageResponseLogin(true, reqLogin.Id, result == ExitCode.Success));
				break;
			}

			case MessageRequestLogout reqLogout:
			{
				if (_isLoggedIn)
				{
					_userService.Logout(this);
					_isLoggedIn = false;
					UserId = -1;
					SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.Success));
				}
				else
				{
					SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.UserNotLoggedIn));
				}

				break;
			}

			case MessageRequestCreateVm reqCreateVm:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseCreateVm(true, reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					break;
				}
			
				string vmNameTrimmed = reqCreateVm.Name.Trim();
				result = await _virtualMachineService.CreateVirtualMachineAsync(UserId, vmNameTrimmed,
					reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, reqCreateVm.BootMode);
				
				if (result == ExitCode.VmAlreadyExists)
				{
					SendResponse(new MessageResponseCreateVm(true, reqCreateVm.Id, MessageResponseCreateVm.Status.VmAlreadyExists));
					break;
				}

				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateVm(true, reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					break;				
				}

				int id = await _databaseService.GetVmIdAsync(UserId, vmNameTrimmed);		/* Must be valid because we just successfully created the VM */
				SendResponse(new MessageResponseCreateVm(true,  reqCreateVm.Id, MessageResponseCreateVm.Status.Success, id));
				
				await _userService.NotifyVirtualMachineCreatedAsync(
					new VmGeneralDescriptor(id, vmNameTrimmed, reqCreateVm.OperatingSystem, VmState.ShutDown)
				);
				
				break;
			}

			case MessageRequestDeleteVm reqDeleteVm:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.Failure));
					break;
				}

				if (await _virtualMachineService.GetVmStateAsync(reqDeleteVm.VmId) != VmState.ShutDown)
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.VirtualMachineIsRunning));
					break;					
				}

				if (await _databaseService.IsVmExistsAsync(reqDeleteVm.VmId))
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.Success));
					
					/* First notifying of VM deletion and then deleting the VM, because this function depends on the VM being in the database. */
					await _userService.NotifyVirtualMachineDeletedAsync(reqDeleteVm.VmId);
				
					/* Must succeed because the VM exists. */
					await _databaseService.DeleteVmAsync(reqDeleteVm.VmId);
				}
				else
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.Failure));
				}
				
				break;
			}

			case MessageRequestListVms reqListVms:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseListVms(true, reqListVms.Id, MessageResponseListVms.Status.Failure));
					break;
				}
				
				VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(UserId);
				if (vms == null)
				{
					SendResponse(new MessageResponseListVms(true, reqListVms.Id, MessageResponseListVms.Status.Failure));
					break;
				}
				
				SendResponse(new MessageResponseListVms(true, reqListVms.Id, MessageResponseListVms.Status.Success, vms));
				
				break;
			}

			case MessageRequestCheckVmExist reqCheckVmExist:
			{
				SendResponse(new MessageResponseCheckVmExist(true,  reqCheckVmExist.Id, 
					_isLoggedIn && await _virtualMachineService.IsVmExistsAsync(UserId, reqCheckVmExist.Name.Trim()))
				);
				break;
			}

			case MessageRequestVmStartup reqVmStartup:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
					break;
				}

				result = await _virtualMachineService.PowerOnVirtualMachineAsync(reqVmStartup.VmId);
				if (result == ExitCode.Success)
				{
					VmGeneralDescriptor? descriptor = await _databaseService.GetVmGeneralDescriptorAsync(reqVmStartup.VmId);
					if (descriptor == null)
					{
						SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
						await _virtualMachineService.PowerOffAndDestroyOnTimeoutAsync(reqVmStartup.VmId);
						break;
					}
					
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Success));
					
					_virtualMachineService.SubscribeToVmPoweredOff(reqVmStartup.VmId, OnVirtualMachinePoweredOffOrCrashed);
					_virtualMachineService.SubscribeToVmCrashed(reqVmStartup.VmId, OnVirtualMachinePoweredOffOrCrashed);
				} 
				else if (result == ExitCode.VmAlreadyRunning)
				{
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.VmAlreadyRunning));
				}
				else
				{
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
				}
				
				break;
			}

			case MessageRequestVmShutdown reqVmShutdown:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
				}
				
				Task<ExitCode> task = _virtualMachineService.PowerOffVirtualMachineAsync(reqVmShutdown.VmId);
				if (task.IsCompleted)
				{
					if (task.Result == ExitCode.Success)
					{
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
					}
					else if (task.Result == ExitCode.VmIsShutDown)
					{
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.VmIsShutDown));
					}
					else
					{
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
					}
				}
				else
				{
					SendResponse(new  MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
				}
				
				break;
			}

			case MessageRequestVmForceOff reqVmForceOff:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmForceOff(true, reqVmForceOff.Id, MessageResponseVmForceOff.Status.Failure));
					break;
				}

				result = _virtualMachineService.ForceOffVirtualMachine(reqVmForceOff.VmId);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseVmForceOff(true, reqVmForceOff.Id, MessageResponseVmForceOff.Status.Success));
				}
				else if (result == ExitCode.VmIsShutDown)
				{
					SendResponse(new MessageResponseVmForceOff(true, reqVmForceOff.Id, MessageResponseVmForceOff.Status.VmIsShutDown));
				}
				else
				{
					SendResponse(new MessageResponseVmForceOff(true, reqVmForceOff.Id, MessageResponseVmForceOff.Status.Failure));
				}
				
				break;
			}

			case MessageRequestVmStreamStart reqVmStreamStart:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, 
						MessageResponseVmStreamStart.Status.Failure));
					break;
				}

				if (_streamVmId != -1)
				{
					if (_streamVmId == reqVmStreamStart.VmId)
					{
						PixelFormat? pixelsFmt = _virtualMachineService.GetScreenStreamPixelFormat(reqVmStreamStart.VmId);
						if (pixelsFmt != null)
						{
							SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id,
								MessageResponseVmStreamStart.Status.AlreadyStreaming, pixelsFmt));
						
							_virtualMachineService.EnqueueGetFullFrame(reqVmStreamStart.VmId);
						}
						else
						{
							SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id,
								MessageResponseVmStreamStart.Status.Failure));			
						}
						
						break;
					}

					_virtualMachineService.UnsubscribeFromVmNewFrameReceived(_streamVmId, OnVmNewFrame);
					_virtualMachineService.UnsubscribeFromVmAudioPacketReceived(_streamVmId, OnVmNewAudioPacket);
					
					_streamVmId = -1;
				}
				
				result = _virtualMachineService.SubscribeToVmNewFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, 
						MessageResponseVmStreamStart.Status.Failure));
					break;
				}
				result = _virtualMachineService.SubscribeToVmAudioPacketReceived(reqVmStreamStart.VmId, OnVmNewAudioPacket);
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, 
						MessageResponseVmStreamStart.Status.Failure));
					
					_virtualMachineService.UnsubscribeFromVmNewFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
					break;
				}
				
				PixelFormat pixelFormat = _virtualMachineService.GetScreenStreamPixelFormat(reqVmStreamStart.VmId)!;

				SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id,
					MessageResponseVmStreamStart.Status.Success, pixelFormat));
					
				_streamVmId = reqVmStreamStart.VmId;
				_virtualMachineService.EnqueueGetFullFrame(reqVmStreamStart.VmId);
				break;
			}

			case MessageRequestVmStreamStop reqVmStreamStop:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmStreamStop(true, reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Failure));
					break;
				}

				if (_streamVmId == -1)
				{
					SendResponse(new MessageResponseVmStreamStop(true, reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.StreamNotRunning));
					break;
				}
				
				result = _virtualMachineService.UnsubscribeFromVmNewFrameReceived(reqVmStreamStop.VmId, OnVmNewFrame);
				_virtualMachineService.UnsubscribeFromVmAudioPacketReceived(reqVmStreamStop.VmId, OnVmNewAudioPacket);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseVmStreamStop(true, reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Success));
					_streamVmId = -1;
				} 
				else if (result == ExitCode.VmScreenStreamNotRunning)	/* Should not happen. Doing it for safety. */
				{
					SendResponse(new MessageResponseVmStreamStop(true, reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.StreamNotRunning));
					_streamVmId = -1;
				}
				else
				{
					SendResponse(new MessageResponseVmStreamStop(true, reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Failure));
				}
				break;
			}
			
			case MessageRequestCreateDriveOs reqCreateDrive:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseCreateDriveOs(true, reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Failure));
					break;
				}
				
				string driveNameTrimmed = reqCreateDrive.Name.Trim();

				result = await _driveService.CreateOperatingSystemDriveAsync(UserId, driveNameTrimmed, reqCreateDrive.OperatingSystem, reqCreateDrive.Size);
				
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDriveOs(true,  reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.DriveAlreadyExists));
					break;			
				}
				if (result == ExitCode.Success)
				{
					/* Must succeed because the drive was created successfully */
					int driveId = await _driveService.GetDriveIdAsync(UserId, driveNameTrimmed);		
					SendResponse(new MessageResponseCreateDriveOs(true, reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Success, driveId));
					
					await _userService.NotifyDriveCreatedAsync(
						new DriveGeneralDescriptor(
							driveId, 
							driveNameTrimmed, 
							reqCreateDrive.Size, 
							_driveService.GetDriveSectorSize(driveId),
							reqCreateDrive.OperatingSystem == OperatingSystem.MiniCoffeeOS 
								? DriveType.Floppy 
								: DriveType.Disk,
							_driveService.GetDrivePartitionTableType(driveId)
						)
					);
					
					break;				
				}

				SendResponse(new MessageResponseCreateDriveOs(true, reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Failure));
				break;
			}

			case MessageRequestConnectDrive reqConnectDrive:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseConnectDrive(true, reqConnectDrive.Id, MessageResponseConnectDrive.Status.Failure));
					break;
				}

				result = await _driveService.ConnectDriveAsync(reqConnectDrive.DriveId, reqConnectDrive.VmId);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseConnectDrive(true, reqConnectDrive.Id, MessageResponseConnectDrive.Status.Success));
					await _userService.NotifyDriveConnected(reqConnectDrive.DriveId, reqConnectDrive.VmId);
				}
				else if (result == ExitCode.DriveConnectionAlreadyExists)
				{
					SendResponse(new MessageResponseConnectDrive(true, reqConnectDrive.Id, MessageResponseConnectDrive.Status.AlreadyConnected));
				}
				else
				{
					SendResponse(new MessageResponseConnectDrive(true, reqConnectDrive.Id, MessageResponseConnectDrive.Status.Failure));
				}
				
				break;
			}

			case MessageRequestDisconnectDrive reqDisconnectDrive:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDisconnectDrive(true, reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Failure));
					break;
				}

				result = await _driveService.DisconnectDriveAsync(reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseDisconnectDrive(true, reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Success));
					await _userService.NotifyDriveDisconnected(reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId);
				}
				else if (result == ExitCode.DriveConnectionAlreadyExists)
					SendResponse(new MessageResponseDisconnectDrive(true, reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.NotConnected));
				else
					SendResponse(new MessageResponseDisconnectDrive(true, reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Failure));
				
				break;
			}

			case MessageRequestListDriveConnections reqListDriveConnections:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseListDriveConnections(true, reqListDriveConnections.Id, MessageResponseListDriveConnections.Status.Failure));
					break;
				}
				
				DriveConnection[]? connections = await _databaseService.GetDriveConnectionsOfUserAsync(UserId);
				if (connections == null)
				{
					SendResponse(new MessageResponseListDriveConnections(true, reqListDriveConnections.Id, 
						MessageResponseListDriveConnections.Status.Failure));
					break;				
				}
			
				SendResponse(new MessageResponseListDriveConnections(true, reqListDriveConnections.Id, 
					MessageResponseListDriveConnections.Status.Success, connections));
				
				break;
			}
			
			case MessageRequestListDrives reqListDrives:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					break;
				}
				
				DriveGeneralDescriptor[]? descriptors = await _driveService.GetDriveGeneralDescriptorsOfUserAsync(UserId);
				if (descriptors == null)
				{
					SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					break;
				}
				
				SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Success, descriptors));

				break;
			}

			case MessageRequestListPathItems reqListPathItems:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, MessageResponseListPathItems.Status.Failure));
					break;
				}
				
				PathItem[]? items = _driveService.ListItems(reqListPathItems.DriveId, reqListPathItems.Path);
				if (items == null)
				{
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, MessageResponseListPathItems.Status.InvalidPath));
				}
				else
				{
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, items));
				}
				
				break;
			}

			case MessageRequestDownloadItem reqDownloadItem:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, MessageResponseDownloadItem.Status.Failure));
					break;
				}

				ItemStream? stream = _driveService.GetItemStream(reqDownloadItem.DriveId, reqDownloadItem.Path);
				if (stream == null)
				{
					SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, MessageResponseDownloadItem.Status.NoSuchItem));
					break;
				}
			
				Guid streamGuid = Guid.NewGuid();
				SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, 
					MessageResponseDownloadItem.Status.Success, streamGuid, stream.Stream.Length)
				);

				/* 30 MiB/sec */
				byte[] buffer = new byte[Math.Min(30 * 1024 * 1024 / 10, stream.Stream.Length)];
				while (stream.Stream.Position < stream.Stream.Length)
				{
					int readSize = (int)Math.Min(buffer.Length, (stream.Stream.Length - stream.Stream.Position));
					
					await stream.Stream.ReadExactlyAsync(buffer, 0, readSize);
					
					SendInfo(new MessageInfoDownloadItemData(true, streamGuid, stream.Stream.Position - buffer.Length, buffer[..readSize]));
					await Task.Delay(100);
				}
				
				stream.Dispose();
				break;
			}

			case MessageRequestDeleteItem reqDeleteItem:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDeleteItem(true, reqDeleteItem.Id, MessageResponseDeleteItem.Status.Failure));
					break;
				}
				
				await _userService.NotifyItemDeletedAsync(reqDeleteItem.DriveId, reqDeleteItem.Path);
				
				result = await _driveService.DeleteItemAsync(reqDeleteItem.DriveId, reqDeleteItem.Path);

				if (result == ExitCode.Success)
					SendResponse(new MessageResponseDeleteItem(true, reqDeleteItem.Id, MessageResponseDeleteItem.Status.Success));
				else if (result == ExitCode.InvalidPath)
					SendResponse(new MessageResponseDeleteItem(true, reqDeleteItem.Id, MessageResponseDeleteItem.Status.NoSuchItem));
				else
					SendResponse(new MessageResponseDeleteItem(true, reqDeleteItem.Id, MessageResponseDeleteItem.Status.Failure));
				
				break;
			}

			default:
			{
				result = ExitCode.Success;
				break;
			}
		}

		switch (result)
		{
			case ExitCode.DisconnectedFromServer:
			{
				HandleSuddenDisconnection();
				break;
			}
		}
	}

	/// <summary>
	/// Processes message info from the client.
	/// </summary>
	/// <param name="info">The sent message info that should be processed. info != null. &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp)</param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the client. <br/>
	/// A message of info type was received - should be processed. info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp) <br/>
	/// Postcondition: Info is considered processed.
	/// </remarks>
	protected override async Task ProcessInfoAsync(Message info)
	{
		await base.ProcessInfoAsync(info);

		ExitCode result = ExitCode.Success;
		switch (info)
		{
			case MessageInfoIdentifyUdp infoIdentifyUdp:
			{
				if (IsUdpMessagingRunning || UdpSocket!.RemoteEndPoint != null)
					break;

				IPAddress remoteIp = ((IPEndPoint)TcpSocket!.RemoteEndPoint!).Address;
				IPEndPoint udpRemote = new IPEndPoint(remoteIp, infoIdentifyUdp.Port);

				try
				{
					await UdpSocket.ConnectAsync(udpRemote);
				}
				catch (Exception)
				{
					break;
				}
				
				StartUdp();
				break;
			}
			case MessageInfoPointerMoved infoPointerMoved:
			{
				if (!_isLoggedIn) break;
				result = _virtualMachineService.EnqueuePointerMovement(infoPointerMoved.VmId, infoPointerMoved.Position);
				break;
			}
			case MessageInfoPointerButtonEvent infoPointerButtonEvent:
			{
				if (!_isLoggedIn) break;
				result = _virtualMachineService.EnqueuePointerButtonEvent(infoPointerButtonEvent.VmId,
					infoPointerButtonEvent.Position, infoPointerButtonEvent.PressedButtons);
				break;
			}
			case MessageInfoKeyboardKeyEvent infoKeyboardKeyEvent:
			{
				if (!_isLoggedIn) break;
				result = _virtualMachineService.EnqueueKeyboardKeyEvent(infoKeyboardKeyEvent.VmId,
					infoKeyboardKeyEvent.Key, infoKeyboardKeyEvent.KeyDown);
				break;
			}
		}
		
		switch (result)
		{
			case ExitCode.DisconnectedFromServer:
			{
				HandleSuddenDisconnection();
				break;
			}
		}
	}

	/// <summary>
	/// Notifies the client that a virtual machine was created.
	/// </summary>
	/// <param name="descriptor">A descriptor of the new virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: A new virtual machine was created. Service initialized and connected to client. descriptor != null. <br/>
	/// Postcondition: Client notified of the new virtual machine.
	/// </remarks>
	public void NotifyVirtualMachineCreated(VmGeneralDescriptor descriptor) =>
		SendInfo(new MessageInfoVmCreated(true, descriptor));
	
	/// <summary>
	/// Notifies the client that a virtual machine was deleted.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was deleted. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was deleted. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client notified of the virtual machine deletion event.
	/// </remarks>
	public void NotifyVirtualMachineDeleted(int vmId) =>
		SendInfo(new MessageInfoVmDeleted(true, vmId));

	/// <summary>
	/// Notifies the client that a virtual machine was powered on.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was powered on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered on. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client is notified that the virtual machine was powered on.
	/// </remarks>
	public void NotifyVirtualMachinePoweredOn(int vmId) =>
		SendInfo(new MessageInfoVmPoweredOn(true, vmId));
	
	/// <summary>
	/// Notifies the client that a virtual machine was powered off.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was powered off. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered off. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client is notified that the virtual machine was powered off.
	/// </remarks>
	public void NotifyVirtualMachinePoweredOff(int vmId) =>
		SendInfo(new MessageInfoVmPoweredOff(true, vmId));

	/// <summary>
	/// Notifies the client that a virtual machine has crashed.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that has crashed. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has crashed. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client is notified that the virtual machine has crashed.
	/// </remarks>
	public void NotifyVirtualMachineCrashed(int vmId) =>
		SendInfo(new MessageInfoVmCrashed(true, vmId));
	
	/// <summary>
	/// Notifies the client that a drive was created.
	/// </summary>
	/// <param name="descriptor">A descriptor of the new drive. descriptor != null.</param>
	/// <remarks>
	/// Precondition: A new drive was created. Service initialized and connected to client. descriptor != null. <br/>
	/// Postcondition: Client is notified of the new drive.
	/// </remarks>
	public void NotifyDriveCreated(DriveGeneralDescriptor descriptor) =>
		SendInfo(new MessageInfoDriveCreated(true, descriptor));

	/// <summary>
	/// Notifies the client that an item was deleted.
	/// </summary>
	/// <param name="driveId">The ID of the drive that held the deleted item. driveId >= 1.</param>
	/// <param name="path">The path on the drive that pointed to the item. path != null.</param>
	/// <remarks>
	/// Precondition: An item was deleted. Service initialized and connected to client. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: Client is notified that the item was deleted.
	/// </remarks>
	public void NotifyItemDeleted(int driveId, string path) =>
		SendInfo(new MessageInfoItemDeleted(true, driveId, path));

	/// <summary>
	/// Notifies the client that a drive was connected to a virtual machine. (New VM-drive connection created)
	/// </summary>
	/// <param name="driveId">The ID of the drive that was connected. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive was connected to. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A drive was connected to a virtual machine. Service initialized and connected to client.
	/// driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: Client is notified that the drive is connected to the virtual machine.
	/// </remarks>
	public void NotifyDriveConnected(int driveId, int vmId) =>
		SendInfo(new MessageInfoDriveConnected(true, driveId, vmId));

	/// <summary>
	/// Notifies the client that a drive was disconnected from a virtual machine. (Drive connected removed)
	/// </summary>
	/// <param name="driveId">The ID of the drive that was disconnected. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine that the drive was disconnected from. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A drive was disconnected from a virtual machine. Service initialized and connected to client.
	/// driveId >= 1. &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: Client is notified that the drive was disconnected from the virtual machine.
	/// </remarks>
	public void NotifyDriveDisconnected(int driveId, int vmId) =>
		SendInfo(new MessageInfoDriveDisconnected(true, driveId, vmId));
	
	/// <summary>
	/// Handles what happens after a disconnection. (sudden or regular disconnection)
	/// </summary>
	/// <remarks>
	/// Precondition: A disconnection has occured. <br/>
	/// Postcondition: This connection is dropped - client considered as not connected.
	/// </remarks>
	protected override void AfterDisconnection()
	{
		if (!_hasDisconnected) /* To prevent recursion */
		{
			_hasDisconnected = true;
			base.AfterDisconnection();
			Disconnect();
			Disconnected?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>
	/// Handles a new frame of the screen of a virtual machine. Sends it to the client.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="frame">The new frame. frame != null.</param>
	/// <remarks>
	/// Precondition: A new frame of a virtual machine was received. The user is logged in, and has watch permissions for the virtual machine. frame != null. <br/>
	/// Postcondition: The frame is sent to the client. If the user is not logged in or doesn't have watch permissions, the frame is not sent.
	/// </remarks>
	private void OnVmNewFrame(object? sender, VirtualMachineFrame frame)
	{
		if (!_isLoggedIn || _streamVmId != frame.VmId) return;
		
		MessageInfoVmScreenFrame frameMessage = new MessageInfoVmScreenFrame(true, frame.VmId, frame.Size, frame.CompressedFramebuffer);
		SendInfo(frameMessage);
	}

	/// <summary>
	/// Handles a new audio packet of a virtual machine. Sends it to the client.
	/// </summary>
	/// <param name="sender">The virtual machine that received the audio packet. sender != null &amp;&amp; sender is VirtualMachine.</param>
	/// <param name="packet">The new audio packet. packet != null.</param>
	/// <remarks>
	/// Precondition: A new audio packet of a virtual machine was received. The user is logged in, and has watch permissions for the virtual machine.
	/// sender != null. &amp;&amp; sender is VirtualMachine &amp;&amp; packet != null.<br/>
	/// Postcondition: The audio packet is sent to the client. If the user is not logged in or doesn't have watch permissions, the packet is not sent.
	/// </remarks>
	private void OnVmNewAudioPacket(object? sender, byte[] packet)
	{
		if (sender == null || sender is not VirtualMachine vm || !_isLoggedIn || _streamVmId != vm.Id) return;

		MessageInfoVmAudioPacket audioMessage = new MessageInfoVmAudioPacket(true, _streamVmId, packet);
		SendInfo(audioMessage);
	}
	
	/// <summary>
	/// Handles the event of a virtual machine being shut down.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was shut down. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has been powered off. id >= 1. <br/>
	/// Postcondition: The event is handled, client receives information if needed.
	/// </remarks>
	private void OnVirtualMachinePoweredOffOrCrashed(object? sender, int id)
	{
		if (!_isLoggedIn || id < 1) return;
		
		if (id == _streamVmId)
		{
			_streamVmId = -1;
		}
	}
}
