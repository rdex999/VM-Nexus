using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Services;
using Server.VirtualMachines;
using Shared;
using Shared.Networking;

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
	private const string ServerRootDirectory = "../../../";

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
		udpSocket.Connect(tcpSocket.RemoteEndPoint!);
		Initialize(tcpSocket, udpSocket);
		IsServiceInitialized = true;
		TcpCommunicationThread!.Start();
		UdpCommunicationThread!.Start();
		MessageSenderThread!.Start();
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

					SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(UserId);
					if (vms == null)
					{
						break;
					}

					foreach (SharedDefinitions.VmGeneralDescriptor vm in vms)
					{
						if (vm.State == SharedDefinitions.VmState.Running)
						{
							_virtualMachineService.SubscribeToVmPoweredOff(vm.Id, OnVirtualMachinePoweredOff);
							_virtualMachineService.SubscribeToVmCrashed(vm.Id, OnVirtualMachineCrashed);
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
					await _userService.LogoutAsync(this);
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
				
				SendInfo(new MessageInfoVmCreated(true, 
					new SharedDefinitions.VmGeneralDescriptor(id, vmNameTrimmed, reqCreateVm.OperatingSystem, SharedDefinitions.VmState.ShutDown)
				));
				break;
			}

			case MessageRequestDeleteVm reqDeleteVm:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.Failure));
					break;
				}

				if (await _virtualMachineService.GetVmStateAsync(reqDeleteVm.VmId) != SharedDefinitions.VmState.ShutDown)
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.VirtualMachineIsRunning));
					break;					
				}

				result = await _databaseService.DeleteVmAsync(reqDeleteVm.VmId);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseDeleteVm(true, reqDeleteVm.Id, MessageResponseDeleteVm.Status.Success));
					SendInfo(new MessageInfoVmDeleted(true, reqDeleteVm.VmId));
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
				
				SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(UserId);
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
					SharedDefinitions.VmGeneralDescriptor? descriptor = await _databaseService.GetVmGeneralDescriptorAsync(reqVmStartup.VmId);
					if (descriptor == null)
					{
						SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
						await _virtualMachineService.PowerOffAndDestroyOnTimeoutAsync(reqVmStartup.VmId);
						break;
					}
					
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Success));
					SendInfo(new MessageInfoVmPoweredOn(true, reqVmStartup.VmId));
					
					_virtualMachineService.SubscribeToVmPoweredOff(reqVmStartup.VmId, OnVirtualMachinePoweredOff);
					_virtualMachineService.SubscribeToVmCrashed(reqVmStartup.VmId, OnVirtualMachineCrashed);
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
			
			case MessageRequestCreateDrive reqCreateDrive:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
					break;
				}
				
				string driveNameTrimmed = reqCreateDrive.Name.Trim();

				if (reqCreateDrive.OperatingSystem == SharedDefinitions.OperatingSystem.Other)
				{
					/* Temporary */
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
				}
				else
				{
					result = await _driveService.CreateOperatingSystemDriveAsync(UserId, driveNameTrimmed, reqCreateDrive.OperatingSystem, reqCreateDrive.Size);
				}
				
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDrive(true,  reqCreateDrive.Id, MessageResponseCreateDrive.Status.DriveAlreadyExists));
					break;			
				}
				if (result == ExitCode.Success)
				{
					int id = await _driveService.GetDriveIdAsync(UserId, driveNameTrimmed);	/* Must succeed because the drive was created successfully */
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Success, id));
					
					SendInfo(new MessageInfoDriveCreated(true, 
						new SharedDefinitions.DriveGeneralDescriptor(id, driveNameTrimmed, reqCreateDrive.Size, reqCreateDrive.Type)
					));
					break;				
				}

				SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
				break;
			}

			case MessageRequestDeleteDrive reqDeleteDrive:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseDeleteDrive(true, reqDeleteDrive.Id, MessageResponseDeleteDrive.Status.Failure));
					break;
				}

				result = await _driveService.DeleteDriveAsync(reqDeleteDrive.DriveId);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseDeleteDrive(true, reqDeleteDrive.Id, MessageResponseDeleteDrive.Status.Success));
					SendInfo(new MessageInfoDriveDeleted(true, reqDeleteDrive.DriveId));
				}
				else
				{
					SendResponse(new MessageResponseDeleteDrive(true, reqDeleteDrive.Id, MessageResponseDeleteDrive.Status.Failure));
				}
				
				/* TODO: Send info drive deleted. */
				
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
					SendInfo(new MessageInfoDriveConnected(true, reqConnectDrive.DriveId, reqConnectDrive.VmId));
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

			case MessageRequestListDriveConnections reqListDriveConnections:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseListDriveConnections(true, reqListDriveConnections.Id, MessageResponseListDriveConnections.Status.Failure));
					break;
				}
				
				SharedDefinitions.DriveConnection[]? connections = await _databaseService.GetDriveConnectionsOfUserAsync(UserId);
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
				
				SharedDefinitions.DriveGeneralDescriptor[]? descriptors = await _databaseService.GetDriveGeneralDescriptorsOfUserAsync(UserId);
				if (descriptors == null)
				{
					SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					break;
				}
				
				SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Success, descriptors));

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
	private void OnVirtualMachinePoweredOff(object? sender, int id)
	{
		if (!_isLoggedIn || id < 1) return;
		
		SendInfo(new MessageInfoVmPoweredOff(true, id));

		if (id == _streamVmId)
		{
			_streamVmId = -1;
		}
	}
	
	/// <summary>
	/// Handles the event of a virtual machine crashing
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was shut down. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has crashed. id >= 1. <br/>
	/// Postcondition: The event is handled, client receives information if needed.
	/// </remarks>
	private void OnVirtualMachineCrashed(object? sender, int id)
	{
		if (!_isLoggedIn || id < 1) return;
		
		SendInfo(new MessageInfoVmCrashed(true, id));
		
		if (id == _streamVmId)
		{
			_streamVmId = -1;
		}
	}
}
