using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Server.Drives;
using Server.Services;
using Server.VirtualMachines;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Server.Models;

public sealed class ClientConnection : MessagingService
{
	public event EventHandler? Disconnected;
	public Guid ClientId { get; private init; }
	public User? ActualUser { get; private set; }		/* The user itself - if logged in as sub-user, this is the owner. */
	public SubUser? User { get; private set; }			/* The sub-user. Null if not logged in as a sub-user. */
	public User? ActionUser								/* The user that actions such as creating a VM, deleting a drive, will be done as. */
	{
		get
		{
			if (User != null)
				return User;
			
			if (ActualUser != null)
				return ActualUser;

			return null;
		}
	}
	public bool IsLoggedIn => ActualUser != null;
	public bool IsLoggedInAsSubUser => User != null && ActualUser != null;	
	
	private readonly DatabaseService _databaseService;
	private readonly UserService _userService;
	private readonly VirtualMachineService _virtualMachineService;
	private readonly DriveService _driveService;
	private readonly AccountService _accountService;
	private readonly ConcurrentDictionary<Guid, TransferHandler> _downloads;
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
	/// <param name="accountService">A reference to the account service. accountService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server.
	/// socket != null &amp;&amp; userService != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null.
	/// &amp;&amp; driveService != null &amp;&amp; accountService != null. <br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(Socket tcpSocket, DatabaseService databaseService, UserService userService, 
		VirtualMachineService virtualMachineService, DriveService driveService, AccountService accountService)
		: base(true)
	{
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		_accountService = accountService;
		_downloads = new ConcurrentDictionary<Guid, TransferHandler>();
		ClientId = Guid.NewGuid();

		Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		udpSocket.Bind(new IPEndPoint(IPAddress.Any, SharedDefinitions.ServerUdpPort));

		TcpSocket = tcpSocket;
		UdpSocket = udpSocket;

		_ = InitializeAsync();
	}

	/// <summary>
	/// Creates and initializes the ClientConnection object.
	/// </summary>
	/// <param name="socket">The socket on which the client has connected. socket != null.</param>
	/// <param name="databaseService">A reference to the database service. databaseService != null.</param>
	/// <param name="userService">A reference to the user service. userService != null.</param>
	/// <param name="virtualMachineService">A reference to the virtual machine service. virtualMachineService != null.</param>
	/// <param name="driveService">A reference to the drive service. driveService != null.</param>
	/// <param name="accountService">A reference to the account service. accountService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server.
	/// socket != null &amp;&amp; userService != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null. &amp;&amp; driveService != null.<br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(WebSocket socket, DatabaseService databaseService, UserService userService, 
		VirtualMachineService virtualMachineService, DriveService driveService, AccountService accountService)
		: base(true)
	{
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		_accountService = accountService;
		_downloads = new ConcurrentDictionary<Guid, TransferHandler>();
		ClientId = Guid.NewGuid();

		WebSocket = socket;
		
		IsServiceInitialized = true;
		
		StartTcp();
	}

	/// <summary>
	/// Initialize this client connection, TLS encryption and authenticate as the client.
	/// </summary>
	/// <remarks>
	/// Precondition: TcpSocket connected to the client. Client is not running on browser. <br/>
	/// Postcondition: On success, this connection is initialized, TcpSslStream is encrypted and can be used for secure communication,
	/// and the returned exit code indicates success. On failure, this connection is dropped.
	/// </remarks>
	private async Task InitializeAsync()
	{
		if (WebSocket != null)
			return;
		
		if (TcpSocket == null)
		{
			Disconnect();
			return;
		}
		
		NetworkStream networkStream = new NetworkStream(TcpSocket, true);
		TcpSslStream = new SslStream(networkStream, false);

		string password = await File.ReadAllTextAsync("../../../Keys/server.pswd");
		X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(
			"../../../Keys/server.pfx", password,
			X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable
		);

		SslServerAuthenticationOptions options = new SslServerAuthenticationOptions()
		{
			ServerCertificate = certificate,
			EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
			ClientCertificateRequired = false,
		};

		try
		{
			await TcpSslStream.AuthenticateAsServerAsync(options);
		}
		catch (Exception)
		{
			Disconnect();
			return;
		}
		
		IsServiceInitialized = true;
	
		/* UDP will be started once a MessageInfoIdentifyUdp is received (using TCP socket) from the client. */
		StartTcp();

		ResetUdpCrypto();
	}
	
	/// <summary>
	/// Checks if the current login state and user has a permission. <br/>
	/// A user not logged in - has no permissions. <br/>
	/// A user logged in as itself - has all permissions. <br/>
	/// A user logged in as a sub-user (logged into a sub-user, from the owners account) - has the owners permissions.
	/// </summary>
	/// <param name="permission">The permission to check if available.</param>
	/// <returns>True if the permission is available, false otherwise.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns true if the permission is available, false otherwise.
	/// </remarks>
	public bool HasPermission(UserPermissions permission) =>
		(IsLoggedIn && !IsLoggedInAsSubUser) || (IsLoggedInAsSubUser && User!.OwnerPermissions.HasPermission(permission.AddIncluded()));

	/// <summary>
	/// Checks if the current login state and user has a permission and access to a virtual machine. <br/>
	/// A user not logged in - has no permissions. <br/>
	/// A user logged in as itself - has all permissions. <br/>
	/// A user logged in as a sub-user (logged into a sub-user, from the owners account) - has the owners permissions.
	/// </summary>
	/// <param name="permission">The permission to check if available.</param>
	/// <param name="vmId">The ID of the virtual machine to check if the user has access to. vmId >= 1.</param>
	/// <returns>True if the permission is available, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A virtual machine with the given ID exists. vmId >= 1. <br/>
	/// Postcondition: Returns true if the permission is available and the virtual machine is accessible, false otherwise.
	/// </remarks>
	public async Task<bool> HasPermissionVmOwnerAsync(UserPermissions permission, int vmId) => 
		(IsLoggedIn && !IsLoggedInAsSubUser) 
		|| (IsLoggedInAsSubUser && User!.OwnerPermissions.HasPermission(permission.AddIncluded()) && await _databaseService.GetVmOwnerIdAsync(vmId) == User!.Id);
	
	/// <summary>
	/// Checks if the current login state and user has a permission and access to a drive. <br/>
	/// A user not logged in - has no permissions. <br/>
	/// A user logged in as itself - has all permissions. <br/>
	/// A user logged in as a sub-user (logged into a sub-user, from the owners account) - has the owners permissions.
	/// </summary>
	/// <param name="permission">The permission to check if available.</param>
	/// <param name="driveId">The ID of the drive to check if the user has access to. driveId >= 1.</param>
	/// <returns>True if the permission is available, false otherwise.</returns>
	/// <remarks>
	/// Precondition: A drive with the given ID exists. driveId >= 1. <br/>
	/// Postcondition: Returns true if the permission is available and the drive is accessible, false otherwise.
	/// </remarks>
	public async Task<bool> HasPermissionDriveOwnerAsync(UserPermissions permission, int driveId) => 
		(IsLoggedIn && !IsLoggedInAsSubUser) 
		|| (IsLoggedInAsSubUser && User!.OwnerPermissions.HasPermission(permission.AddIncluded()) && await _databaseService.GetDriveOwnerIdAsync(driveId) == User!.Id);
	
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

				MessageResponseCreateAccount.Status status;
				result = await _databaseService.RegisterUserAsync(usernameTrimmed, emailTrimmed, reqCreateAccount.Password);
				if (result == ExitCode.Success)
				{
					ActualUser = await _databaseService.GetUserAsync(usernameTrimmed);	/* Must be valid because created user successfully. */
					User = null;
					if (ActualUser == null)
						SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id,
							MessageResponseCreateAccount.Status.Failure));
					else
					{
						_userService.LoginAsUser(this, ActualUser.Id);
						SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, 
							MessageResponseCreateAccount.Status.Success, ActualUser));
					}
					
					break;
				}
				if (result == ExitCode.UserAlreadyExists)
					status = MessageResponseCreateAccount.Status.UsernameNotAvailable;
				else
					status = MessageResponseCreateAccount.Status.Failure;
				
				SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, status));
				break;
			}

			case MessageRequestDeleteAccount reqDeleteAccount:
			{
				if (!IsLoggedIn)
				{
					SendResponse(new MessageResponseDeleteAccount(true, reqDeleteAccount.Id, false));
					break;
				}
				
				if (reqDeleteAccount.UserId == ActionUser!.Id)
				{
					if (IsLoggedInAsSubUser && !HasPermission(UserPermissions.UserDelete))
					{
						SendResponse(new MessageResponseDeleteAccount(true, reqDeleteAccount.Id, false));
						break;
					}
				}
				else
				{
					User? user = await _databaseService.GetUserAsync(reqDeleteAccount.UserId);
					if (user is not SubUser subUser || subUser.OwnerId != ActualUser!.Id 
					                                || !subUser.OwnerPermissions.HasPermission(UserPermissions.UserDelete.AddIncluded()))
					{
						SendResponse(new MessageResponseDeleteAccount(true, reqDeleteAccount.Id, false));
						break;
					}
				}

				result = await _accountService.DeleteAccountAsync(reqDeleteAccount.UserId);
				
				SendResponse(new MessageResponseDeleteAccount(true, reqDeleteAccount.Id, result == ExitCode.Success));
				break;
			}

			case MessageRequestLogin reqLogin:
			{
				string usernameTrimmed = reqLogin.Username.Trim();
				result = await _userService.LoginAsync(usernameTrimmed, reqLogin.Password, this);
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseLogin(true, reqLogin.Id));
					break;
				}

				User = null;
				ActualUser = await _databaseService.GetUserAsync(usernameTrimmed);
				if (ActualUser == null)
				{
					SendResponse(new MessageResponseLogin(true, reqLogin.Id));
					break;
				}				

				SendResponse(new MessageResponseLogin(true, reqLogin.Id, ActualUser));
				break;
			}

			case MessageRequestLogout reqLogout:
			{
				if (IsLoggedIn)
				{
					_userService.Logout(this);
					
					if (IsLoggedInAsSubUser)
						User = null;
					else
						ActualUser = null;
					
					SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.Success, ActualUser));
				}
				else
				{
					SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.UserNotLoggedIn));
				}

				break;
			}

			case MessageRequestLoginSubUser reqLoginSubUser:
			{
				User? user = await _databaseService.GetUserAsync(reqLoginSubUser.UserId);
				if (!IsLoggedIn || IsLoggedInAsSubUser || user is not SubUser subUser || subUser.OwnerId != ActualUser!.Id)
				{
					SendResponse(new MessageResponseLoginSubUser(true, reqLoginSubUser.Id));
					break;
				}

				User = subUser;
				
				_userService.LoginToSubUser(this);
				
				SendResponse(new MessageResponseLoginSubUser(true, reqLoginSubUser.Id, subUser));
				break;
			}

			case MessageRequestCreateSubUser reqCreateSubUser:
			{
				if (!IsLoggedIn || IsLoggedInAsSubUser)
				{
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Failure));
					break;
				}

				if (!Common.IsValidUsername(reqCreateSubUser.Username))
				{
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.InvalidUsernameSyntax));
					break;
				}

				if (!Common.IsValidEmail(reqCreateSubUser.Email))
				{
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.InvalidEmail));
					break;
				}
				
				result = await _databaseService.RegisterUserAsync(ActionUser!.Id, reqCreateSubUser.Permissions,
					reqCreateSubUser.Username, reqCreateSubUser.Email, reqCreateSubUser.Password);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Success));

					User? user = await _databaseService.GetUserAsync(reqCreateSubUser.Username);
					if (user is SubUser subUser)
						_userService.NotifySubUserCreated(subUser);
				}
				else if (result == ExitCode.UserAlreadyExists)
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.UsernameNotAvailable));
				else
					SendResponse(new MessageResponseCreateSubUser(true, reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Failure));
				
				break;
			}

			case MessageRequestListSubUsers reqListSubUsers:
			{
				if (!IsLoggedIn)
				{
					SendResponse(new MessageResponseListSubUsers(true, reqListSubUsers.Id, MessageResponseListSubUsers.Status.Failure));
					break;
				}

				SubUser[]? subUsers = await _databaseService.GetSubUsersAsync(ActionUser!.Id);
				if (subUsers == null)
				{
					SendResponse(new MessageResponseListSubUsers(true, reqListSubUsers.Id, MessageResponseListSubUsers.Status.Failure));
					break;				
				}
				
				SendResponse(new MessageResponseListSubUsers(true, reqListSubUsers.Id, MessageResponseListSubUsers.Status.Success, subUsers));
				break;
			}

			case MessageRequestCreateVm reqCreateVm:
			{
				if (!HasPermission(UserPermissions.VirtualMachineCreate))
				{
					SendResponse(new MessageResponseCreateVm(true, reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					break;
				}
			
				string vmNameTrimmed = reqCreateVm.Name.Trim();
				result = await _virtualMachineService.CreateVirtualMachineAsync(ActionUser!.Id, vmNameTrimmed,
					reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, reqCreateVm.RamSizeMiB, reqCreateVm.BootMode);
				
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

				int id = await _databaseService.GetVmIdAsync(ActionUser.Id, vmNameTrimmed);		/* Must be valid because we just successfully created the VM */
				SendResponse(new MessageResponseCreateVm(true,  reqCreateVm.Id, MessageResponseCreateVm.Status.Success, id));
				
				await _userService.NotifyVirtualMachineCreatedAsync(
					new VmGeneralDescriptor(id, vmNameTrimmed, reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, 
						VmState.ShutDown, reqCreateVm.RamSizeMiB, reqCreateVm.BootMode
					)
				);
				
				break;
			}

			case MessageRequestDeleteVm reqDeleteVm:
			{
				if (!HasPermission(UserPermissions.VirtualMachineDelete) || await _databaseService.GetVmOwnerIdAsync(reqDeleteVm.VmId) != ActionUser!.Id)
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
				if (!HasPermission(UserPermissions.VirtualMachineList))
				{
					SendResponse(new MessageResponseListVms(true, reqListVms.Id, MessageResponseListVms.Status.Failure));
					break;
				}
				
				VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(ActionUser!.Id);
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
				if (!HasPermission(UserPermissions.VirtualMachineList))
				{
					SendResponse(new MessageResponseCheckVmExist(true, reqCheckVmExist.Id, false));
					break;
				}
			
				SendResponse(new MessageResponseCheckVmExist(true,  reqCheckVmExist.Id, 
					IsLoggedIn && await _virtualMachineService.IsVmExistsAsync(ActionUser!.Id, reqCheckVmExist.Name.Trim()))
				);
				break;
			}

			case MessageRequestVmStartup reqVmStartup:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmStartup.VmId))
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
				} 
				else if (result == ExitCode.VmAlreadyRunning)
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.VmAlreadyRunning));
				
				else if (result == ExitCode.InsufficientMemory)
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.ServerStarvation));
				
				else
					SendResponse(new MessageResponseVmStartup(true, reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
				
				break;
			}

			case MessageRequestVmShutdown reqVmShutdown:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmShutdown.VmId))
				{
					SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
					break;
				}
				
				Task<ExitCode> task = _virtualMachineService.PowerOffVirtualMachineAsync(reqVmShutdown.VmId);
				if (task.IsCompleted)
				{
					if (task.Result == ExitCode.Success)
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
					
					else if (task.Result == ExitCode.VmIsShutDown)
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.VmIsShutDown));
					
					else
						SendResponse(new MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
				}
				else
					SendResponse(new  MessageResponseVmShutdown(true, reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
				
				break;
			}

			case MessageRequestVmForceOff reqVmForceOff:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmForceOff.VmId))
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
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineWatch, reqVmStreamStart.VmId))
				{
					SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, MessageResponseVmStreamStart.Status.Failure));
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
			
				if (WebSocket == null)
					result = _virtualMachineService.SubscribeToVmNewBrotliFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
				else
					result = _virtualMachineService.SubscribeToVmNewGzipFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
				
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, 
						MessageResponseVmStreamStart.Status.Failure));
					break;
				}
				
				/* Audio not supported on web and android. */
				if (IsUdpMessagingRunning)
				{
					result = _virtualMachineService.SubscribeToVmAudioPacketReceived(reqVmStreamStart.VmId, OnVmNewAudioPacket);

					if (result != ExitCode.Success)
					{
						SendResponse(new MessageResponseVmStreamStart(true, reqVmStreamStart.Id, MessageResponseVmStreamStart.Status.Failure));

						_virtualMachineService.UnsubscribeFromVmNewFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
						break;
					}
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
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineWatch, reqVmStreamStop.VmId))
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
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveOs(true, reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Failure));
					break;
				}
				
				string driveNameTrimmed = reqCreateDrive.Name.Trim();

				result = await _driveService.CreateOperatingSystemDriveAsync(ActionUser!.Id, driveNameTrimmed, reqCreateDrive.OperatingSystem, reqCreateDrive.Size);
				
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDriveOs(true,  reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.DriveAlreadyExists));
					break;			
				}
				if (result == ExitCode.Success)
				{
					/* Must succeed because the drive was created successfully */
					int driveId = await _driveService.GetDriveIdAsync(ActionUser.Id, driveNameTrimmed);		
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

			case MessageRequestCreateDriveFs reqCreateDriveFs:
			{
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveFs(true, reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Failure));
					break;
				}

				string driveName = reqCreateDriveFs.Name.Trim();
				result = await _driveService.CreateFileSystemDriveAsync(ActionUser!.Id, driveName, reqCreateDriveFs.SizeMb, reqCreateDriveFs.FileSystem);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDriveFs(true, reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Success));
					
					DriveGeneralDescriptor descriptor = (await _driveService.GetDriveGeneralDescriptorAsync(ActionUser.Id, driveName))!;
					await _userService.NotifyDriveCreatedAsync(descriptor);
				}
				else if (result == ExitCode.DriveAlreadyExists)
					SendResponse(new MessageResponseCreateDriveFs(true, reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.DriveAlreadyExists));
				else
					SendResponse(new MessageResponseCreateDriveFs(true, reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Failure));
				
				break;
			}

			case MessageRequestCreateDriveFromImage reqCreateDrive:
			{
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveFromImage(true, reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.Failure));
					break;
				}

				string name = reqCreateDrive.Name.Trim();
				result = await _databaseService.CreateDriveAsync(ActionUser!.Id, name, (int)(reqCreateDrive.Size / 1024UL / 1024UL), reqCreateDrive.Type);
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDriveFromImage(true, reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.DriveAlreadyExists));
					break;				
				}
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDriveFromImage(true, reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.DriveAlreadyExists));
					break;								
				}

				int driveId = await _databaseService.GetDriveIdAsync(ActionUser.Id, name);
				DownloadHandler handler = new DownloadHandler(reqCreateDrive.Size, _driveService.GetDriveFilePath(driveId));
				Guid transferId = CreateTransferId();
				handler.Start(transferId);
				AddTransfer(handler);
				
				SendResponse(new MessageResponseCreateDriveFromImage(true, reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.Success, transferId));

				handler.Completed += async (_, _) =>
				{
					DriveGeneralDescriptor? descriptor = await _driveService.GetDriveGeneralDescriptorAsync(ActionUser.Id, name);
					if (descriptor != null)
						await _userService.NotifyDriveCreatedAsync(descriptor);
				};
				break;
			}

			case MessageRequestConnectDrive reqConnectDrive:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveConnect, reqConnectDrive.DriveId))
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
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveDisconnect, reqDisconnectDrive.DriveId))
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
				if (!HasPermission(UserPermissions.DriveConnectionList))
				{
					SendResponse(new MessageResponseListDriveConnections(true, reqListDriveConnections.Id, MessageResponseListDriveConnections.Status.Failure));
					break;
				}
				
				DriveConnection[]? connections = await _databaseService.GetDriveConnectionsOfUserAsync(ActionUser!.Id);
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
				if (!HasPermission(UserPermissions.DriveList))
				{
					SendResponse(new MessageResponseListDrives(true, reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					break;
				}
				
				DriveGeneralDescriptor[]? descriptors = await _driveService.GetDriveGeneralDescriptorsOfUserAsync(ActionUser!.Id);
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
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemList, reqListPathItems.DriveId))
				{
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, MessageResponseListPathItems.Status.Failure));
					break;
				}
				
				PathItem[]? items = _driveService.ListItems(reqListPathItems.DriveId, reqListPathItems.Path);
				if (items == null)
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, MessageResponseListPathItems.Status.InvalidPath));
				
				else
					SendResponse(new MessageResponseListPathItems(true, reqListPathItems.Id, items));
				
				break;
			}

			case MessageRequestDownloadItem reqDownloadItem:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemDownload, reqDownloadItem.DriveId))
				{
					SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, MessageResponseDownloadItem.Status.Failure));
					break;
				}

				ItemStream? stream = _driveService.GetItemStream(reqDownloadItem.DriveId, reqDownloadItem.Path, FileAccess.Read);
				if (stream == null)
				{
					SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, MessageResponseDownloadItem.Status.NoSuchItem));
					break;
				}
			
				Guid streamGuid = Guid.NewGuid();
				SendResponse(new MessageResponseDownloadItem(true, reqDownloadItem.Id, 
					MessageResponseDownloadItem.Status.Success, streamGuid, (ulong)stream.Stream.Length)
				);

				await Task.Delay(500);
				
				UploadHandler handler = new UploadHandler(this, stream.Stream);
				handler.Start(streamGuid);
				AddTransfer(handler);

				await handler.Task;
				
				stream.Dispose();
				break;
			}

			case MessageRequestUploadFile reqUploadFile:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemCreate, reqUploadFile.DriveId))
				{
					SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					break;
				}

				string trimmedPath = reqUploadFile.Path.Trim().Trim(SharedDefinitions.DirectorySeparators);
				string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);
				if (pathParts.Length == 0 || string.IsNullOrEmpty(pathParts[0]))
				{
					SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					break;
				}
				
				string fileParentDirectory = string.Join('/', pathParts[..^1]);
				if (!await _driveService.ItemExistsAsync(reqUploadFile.DriveId, fileParentDirectory))
				{
					SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.InvalidPath));
					break;
				}
				
				string filename = pathParts[^1];
				string path = fileParentDirectory + "/" + filename;
				int fileCopyNumber = -1;
				while (await _driveService.ItemExistsAsync(reqUploadFile.DriveId, path))
				{
					int index = filename.LastIndexOf('.');
					if (index == -1)
						path = fileParentDirectory + "/" + filename + $" ({++fileCopyNumber})";
					else
						path = $"{fileParentDirectory}/{filename[..index]} ({++fileCopyNumber}).{filename[(index + 1)..]}";
				}
				
				ItemStream? stream = _driveService.GetItemStream(reqUploadFile.DriveId, path, FileAccess.ReadWrite, true);
				if (stream == null)
				{
					SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					break;
				}

				if (stream.MaxSize < reqUploadFile.Size)
				{
					SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.FileTooLarge));
					stream.Dispose();
					await _driveService.DeleteItemAsync(reqUploadFile.DriveId, reqUploadFile.Path);
					break;
				}

				DownloadHandler handler = new DownloadHandler(reqUploadFile.Size, stream.Stream);
				handler.Start(Guid.NewGuid());
				AddTransfer(handler);
				
				SendResponse(new MessageResponseUploadFile(true, reqUploadFile.Id, MessageResponseUploadFile.Status.Success, handler.Id));

				await handler.Task;
				stream.Dispose();

				await _userService.NotifyItemCreatedAsync(reqUploadFile.DriveId, trimmedPath);
				break;
			}

			case MessageRequestCreateDirectory reqCreateDirectory:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemCreate, reqCreateDirectory.DriveId))
				{
					SendResponse(new MessageResponseCreateDirectory(true, reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Failure));
					break;
				}
				
				result = _driveService.CreateDirectory(reqCreateDirectory.DriveId, reqCreateDirectory.Path);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDirectory(true, reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Success));
					await _userService.NotifyItemCreatedAsync(reqCreateDirectory.DriveId, reqCreateDirectory.Path);
				}
				
				else if (result == ExitCode.InvalidPath)
					SendResponse(new MessageResponseCreateDirectory(true, reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.InvalidPath));
				
				else
					SendResponse(new MessageResponseCreateDirectory(true, reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Failure));
					
				break;
			}

			case MessageRequestDeleteItem reqDeleteItem:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemDelete, reqDeleteItem.DriveId))
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
			case MessageInfoIdentifyUdpPort infoIdentifyUdp:
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
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, infoPointerMoved.VmId))
					break;
				
				result = _virtualMachineService.EnqueuePointerMovement(infoPointerMoved.VmId, infoPointerMoved.Position);
				break;
			}
			case MessageInfoPointerButtonEvent infoPointerButtonEvent:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, infoPointerButtonEvent.VmId))
					break;
				
				result = _virtualMachineService.EnqueuePointerButtonEvent(infoPointerButtonEvent.VmId,
					infoPointerButtonEvent.Position, infoPointerButtonEvent.PressedButtons);
				break;
			}
			case MessageInfoKeyboardKeyEvent infoKeyboardKeyEvent:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, infoKeyboardKeyEvent.VmId))
					break;
				
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
	/// Notifies the client that a new sub-user was created.
	/// </summary>
	/// <param name="subUser">The new sub-user that was created. subUser != null.</param>
	/// <remarks>
	/// Precondition: A new sub-user was created. Service initialized and connected to client. subUser != null. <br/>
	/// Postcondition: Client is notified that a new sub-user was created.
	/// </remarks>
	public void NotifySubUserCreated(SubUser subUser) =>
		SendInfo(new MessageInfoSubUserCreated(true, subUser));
	
	/// <summary>
	/// Notifies the client that a user was deleted. (Either a sub-user, or the current user itself)
	/// </summary>
	/// <param name="userId">The ID of the user that was deleted. subUserId >= .</param>
	/// <remarks>
	/// Precondition: A user was deleted. Service initialized and connected to client. userId >= 1. <br/>
	/// Postcondition: Client is notified that a user was deleted.
	/// </remarks>
	public void NotifyUserDeleted(int userId) =>
		SendInfo(new MessageInfoUserDeleted(true, userId));
	
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
	public void NotifyVirtualMachinePoweredOff(int vmId)
	{
		SendInfo(new MessageInfoVmPoweredOff(true, vmId));
		OnVirtualMachinePoweredOffOrCrashed(vmId);
	}

	/// <summary>
	/// Notifies the client that a virtual machine has crashed.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that has crashed. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has crashed. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client is notified that the virtual machine has crashed.
	/// </remarks>
	public void NotifyVirtualMachineCrashed(int vmId)
	{
		SendInfo(new MessageInfoVmCrashed(true, vmId));
		OnVirtualMachinePoweredOffOrCrashed(vmId);
	}
	
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
	/// Notifies the client that a new item was created.
	/// </summary>
	/// <param name="driveId">The ID of the drive that holds the new item. driveId >= 1.</param>
	/// <param name="path">The path on the drive that points to the new item, including the item's name. (filename/directory name) path != null.</param>
	/// <remarks>
	/// Precondition: An item was created. Service initialized and connected to client. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: Client is notified that the item a new item was created.
	/// </remarks>
	public void NotifyItemCreated(int driveId, string path) =>
		SendInfo(new MessageInfoItemCreated(true, driveId, path));
	
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
		if (!IsLoggedIn || _streamVmId != frame.VmId) return;
		
		MessageInfoVmScreenFrame frameMessage = new MessageInfoVmScreenFrame(true, frame.VmId, frame.Size, frame.CompressedFramebuffer);

		if (UdpSocket == null)
		{
			if (TransferLimiter.GetTokens() >= frame.CompressedFramebuffer.Length)
			{
				TransferLimiter.AcquireAsync(frame.CompressedFramebuffer.Length).Wait();
				SendInfo(frameMessage);
			}
		}
		else
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
		if (sender == null || sender is not VirtualMachine vm || !IsLoggedIn || _streamVmId != vm.Id) return;

		MessageInfoVmAudioPacket audioMessage = new MessageInfoVmAudioPacket(true, _streamVmId, packet);
		SendInfo(audioMessage);
	}
	
	/// <summary>
	/// Handles the event of a virtual machine being shut down.
	/// </summary>
	/// <param name="id">The ID of the virtual machine that was shut down. id >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine has been powered off. id >= 1. <br/>
	/// Postcondition: The event is handled, client receives information if needed.
	/// </remarks>
	private void OnVirtualMachinePoweredOffOrCrashed(int id)
	{
		if (id != _streamVmId) 
			return;
		
		_streamVmId = -1;
		_virtualMachineService.UnsubscribeFromVmAudioPacketReceived(id, OnVmNewAudioPacket);
		_virtualMachineService.UnsubscribeFromVmNewFrameReceived(id, OnVmNewFrame);
	}
}