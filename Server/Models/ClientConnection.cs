using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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
	private string _username = string.Empty;
	private readonly DatabaseService _databaseService;
	private bool _hasDisconnected = false;		/* Has the Disconnect function run? */

	/// <summary>
	/// Creates and initializes the ClientConnection object.
	/// </summary>
	/// <param name="socket">
	/// The socket on which the client has connected. socket != null.
	/// </param>
	/// <param name="databaseService">
	/// A reference to the database service. databaseService != null.
	/// </param>
	/// <remarks>
	/// Precondition: Client has connected to the server. socket != null &amp;&amp; databaseService != null. <br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(Socket socket, DatabaseService databaseService)
	{
		_databaseService = databaseService;
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
				bool usernameAvailable = !string.IsNullOrEmpty(reqCheckUsername.Username) && !await _databaseService.IsUserExistAsync(reqCheckUsername.Username);
				SendResponse(new MessageResponseCheckUsername(true, reqCheckUsername.Id, usernameAvailable));
				break;
			}

			case MessageRequestCreateAccount reqCreateAccount:
			{
				if (string.IsNullOrEmpty(reqCreateAccount.Username) || 
				    string.IsNullOrEmpty(reqCreateAccount.Email) ||
				    string.IsNullOrEmpty(reqCreateAccount.Password)) 
				{
					SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, MessageResponseCreateAccount.Status.CredentialsCannotBeEmpty));
					break;
				}

				if (!Common.IsValidEmail(reqCreateAccount.Email))
				{
					SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, MessageResponseCreateAccount.Status.InvalidEmail));
					break;
				}
				
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(reqCreateAccount.Username);
				MessageResponseCreateAccount.Status status;
				if (usernameAvailable)
				{
					ExitCode code = await _databaseService.RegisterUserAsync(reqCreateAccount.Username, reqCreateAccount.Email, reqCreateAccount.Password);
					if (code == ExitCode.Success)
					{
						status = MessageResponseCreateAccount.Status.Success;
						_isLoggedIn = true;
						_username = reqCreateAccount.Username;
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
				bool validLogin = await _databaseService.IsValidLoginAsync(reqLogin.Username, reqLogin.Password);
				SendResponse(new MessageResponseLogin(true, reqLogin.Id, validLogin));
				if (validLogin)
				{
					_isLoggedIn = validLogin;
					_username = reqLogin.Username;

					SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsAsync(_username);
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
					_username = string.Empty;
					await Task.Delay(50);		/* If i dont do this, the client gets a response timeout - like the client doesnt receive the response.. */
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
				if (!_isLoggedIn || string.IsNullOrEmpty(reqCreateVm.Name) ||
				    !Enum.IsDefined(typeof(SharedDefinitions.OperatingSystem), reqCreateVm.OperatingSystem) ||
				    !Enum.IsDefined(typeof(SharedDefinitions.CpuArchitecture), reqCreateVm.CpuArchitecture) ||
				    !Enum.IsDefined(typeof(SharedDefinitions.BootMode), reqCreateVm.BootMode)
				   )
				{
					SendResponse(new MessageResponseCreateVm(true, reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					break;
				}
				
				result = await _databaseService.CreateVmAsync(_username, reqCreateVm.Name, reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, reqCreateVm.BootMode);
				
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
				
				SendResponse(new MessageResponseCreateVm(true,  reqCreateVm.Id, MessageResponseCreateVm.Status.Success));
				
				SharedDefinitions.VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsAsync(_username);
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
					_isLoggedIn && await _databaseService.IsVmExistsAsync(_username, reqCheckVmExist.Name))
				);
				break;
			}

			case MessageRequestCreateDrive reqCreateDrive:
			{
				if (!reqCreateDrive.IsValidRequest() || !_isLoggedIn)
				{
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
					break;
				}

				if (await _databaseService.IsDriveExistsAsync(_username, reqCreateDrive.Name))
				{
					SendResponse(new MessageResponseCreateDrive(true,  reqCreateDrive.Id, MessageResponseCreateDrive.Status.AlreadyExistsWithName));
					break;
				}

				string diskImageName = $"{_username}_{reqCreateDrive.Name}.img";
				
				/* TODO: Handle other drive creation scenarios (not only for MiniCoffeeOS) */
				if (reqCreateDrive.OperatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOS)
				{
					Process process  = new Process()
					{
						StartInfo = new ProcessStartInfo()
						{
							FileName = "/usr/bin/make",
							Arguments = $" -C ../../../MiniCoffeeOS FDA=../DiskImages/{diskImageName} FDA_SIZE={reqCreateDrive.Size}",
						},
					};
					process.Start();
					await process.WaitForExitAsync();
					int exitCode = process.ExitCode;
					process.Dispose();

					if (exitCode != 0)
					{
						SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
						break;
					}
				}
				else
				{
					/* Temporary */
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
					break;
				}
				
				result = await _databaseService.CreateDriveAsync(_username, reqCreateDrive.Name, reqCreateDrive.Size, reqCreateDrive.Type);
				if (result != ExitCode.Success)
				{
					File.Delete($"../../../DiskImages/{diskImageName}");
					SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Failure));
					break;
				}
				
				SendResponse(new MessageResponseCreateDrive(true, reqCreateDrive.Id, MessageResponseCreateDrive.Status.Success));
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
}
