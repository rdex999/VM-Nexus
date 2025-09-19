using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Services;
using Shared;
using Shared.Networking;

namespace Server.Models;

public sealed class ClientConnection : MessagingService
{
	public event EventHandler? Disconnected;
	private bool _isLoggedIn = false;
	private int _userId = -1;
	private readonly DatabaseService _databaseService;
	private readonly VirtualMachineService _virtualMachineService;
	private readonly DriveService _driveService;
	private bool _hasDisconnected = false;		/* Has the Disconnect function run? */
	private const string ServerRootDirectory = "../../../";

	/// <summary>
	/// Creates and initializes the ClientConnection object.
	/// </summary>
	/// <param name="socket">The socket on which the client has connected. socket != null.</param>
	/// <param name="databaseService">A reference to the database service. databaseService != null.</param>
	/// <param name="virtualMachineService">A reference to the virtual machine service. virtualMachineService != null.</param>
	/// <param name="driveService">A reference to the drive service. driveService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server.
	/// socket != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null.<br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(Socket socket, DatabaseService databaseService, VirtualMachineService virtualMachineService, DriveService driveService)
	{
		_databaseService = databaseService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		Initialize(socket);
		IsServiceInitialized = true;
		CommunicationThread!.Start();
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
						_userId = await _databaseService.GetUserIdAsync(usernameTrimmed);		/* Must be valid because created user successfully. */
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
				bool validLogin = await _databaseService.IsValidLoginAsync(usernameTrimmed, reqLogin.Password);
				SendResponse(new MessageResponseLogin(true, reqLogin.Id, validLogin));
				if (validLogin)
				{
					_isLoggedIn = validLogin;
					_userId = await _databaseService.GetUserIdAsync(usernameTrimmed);		/* Must be valid because the user exists. (IsValidLogin would return false otherwise) */

					SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsAsync(_userId);
					if (vms == null)
					{
						break;
					}

					SendInfo(new MessageInfoVmList(true, vms));
				}
				break;
			}

			case MessageRequestLogout reqLogout:
			{
				if (_isLoggedIn)
				{
					_isLoggedIn = false;
					_userId = -1;
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
				result = await _virtualMachineService.CreateVirtualMachineAsync(_userId, vmNameTrimmed,
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

				int id = await _databaseService.GetVmIdAsync(_userId, vmNameTrimmed);		/* Must be valid because we just successfully created the VM */
				SendResponse(new MessageResponseCreateVm(true,  reqCreateVm.Id, MessageResponseCreateVm.Status.Success, id));
				
				SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsAsync(_userId);
				if (vms == null)
				{
					break;
				}

				SendInfo(new MessageInfoVmList(true, vms));
				break;
			}

			case MessageRequestCheckVmExist reqCheckVmExist:
			{
				SendResponse(new MessageResponseCheckVmExist(true,  reqCheckVmExist.Id, 
					_isLoggedIn && await _virtualMachineService.IsVmExistsAsync(_userId, reqCheckVmExist.Name.Trim()))
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
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Success));
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

			case MessageRequestVmScreenStream reqVmScreenStream:
			{
				if (!_isLoggedIn)
				{
					SendResponse(new MessageResponseVmScreenStream(true, reqVmScreenStream.Id, MessageResponseVmScreenStream.Status.Failure));
					break;
				}
				
				result = _virtualMachineService.StartScreenStream(reqVmScreenStream.VmId, OnVmNewFrame);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseVmScreenStream(true, reqVmScreenStream.Id, MessageResponseVmScreenStream.Status.Success));
				} 
				else if (result == ExitCode.VmScreenScreenAlreadyRunning)
				{
					SendResponse(new MessageResponseVmScreenStream(true, reqVmScreenStream.Id, MessageResponseVmScreenStream.Status.AlreadyStreaming));
				}
				else
				{
					SendResponse(new MessageResponseVmScreenStream(true, reqVmScreenStream.Id, MessageResponseVmScreenStream.Status.Failure));
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
					result = await _driveService.CreateOperatingSystemDriveAsync(_userId, driveNameTrimmed, reqCreateDrive.OperatingSystem, reqCreateDrive.Size);
				}
				
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDrive(true,  reqCreateDrive.Id, MessageResponseCreateDrive.Status.DriveAlreadyExists));
					break;			
				}
				if (result == ExitCode.Success)
				{
					int id = await _driveService.GetDriveIdAsync(_userId, driveNameTrimmed);	/* Must succeed because the drive was created successfully */
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Success, id));
					break;				
				}

				SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
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
				
				MessageResponseConnectDrive.Status status = MessageResponseConnectDrive.Status.Success;
				if (result == ExitCode.DriveConnectionAlreadyExists)
				{
					status = MessageResponseConnectDrive.Status.AlreadyConnected;
				}
				else if (result != ExitCode.Success)
				{
					status = MessageResponseConnectDrive.Status.Failure;
				}
				
				SendResponse(new MessageResponseConnectDrive(true, reqConnectDrive.Id, status));
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

	private void OnVmNewFrame(byte[] framebuffer)
	{
		
	}
}
