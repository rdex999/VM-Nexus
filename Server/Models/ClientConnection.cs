using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Serilog;
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
	public Guid ClientId { get; }
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
	
	private readonly ILogger _logger;
	private readonly DatabaseService _databaseService;
	private readonly UserService _userService;
	private readonly VirtualMachineService _virtualMachineService;
	private readonly DriveService _driveService;
	private readonly AccountService _accountService;
	private bool _hasDisconnected = false;		/* Has the Disconnect function run? */
	private int _streamVmId = -1;

	/// <summary>
	/// Creates and initializes the ClientConnection object.
	/// </summary>
	/// <param name="logger">The logger. logger != null.</param>
	/// <param name="tcpSocket">The socket on which the client has connected. socket != null.</param>
	/// <param name="databaseService">A reference to the database service. databaseService != null.</param>
	/// <param name="userService">A reference to the user service. userService != null.</param>
	/// <param name="virtualMachineService">A reference to the virtual machine service. virtualMachineService != null.</param>
	/// <param name="driveService">A reference to the drive service. driveService != null.</param>
	/// <param name="accountService">A reference to the account service. accountService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server. logger != null &amp;&amp;
	/// socket != null &amp;&amp; userService != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null.
	/// &amp;&amp; driveService != null &amp;&amp; accountService != null. <br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(ILogger logger, Socket tcpSocket, DatabaseService databaseService, UserService userService, 
		VirtualMachineService virtualMachineService, DriveService driveService, AccountService accountService)
		: base(true)
	{
		_logger = logger;
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		_accountService = accountService;
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
	/// <param name="logger">The logger. logger != null.</param>
	/// <param name="socket">The socket on which the client has connected. socket != null.</param>
	/// <param name="databaseService">A reference to the database service. databaseService != null.</param>
	/// <param name="userService">A reference to the user service. userService != null.</param>
	/// <param name="virtualMachineService">A reference to the virtual machine service. virtualMachineService != null.</param>
	/// <param name="driveService">A reference to the drive service. driveService != null.</param>
	/// <param name="accountService">A reference to the account service. accountService != null.</param>
	/// <remarks>
	/// Precondition: Client has connected to the server. logger != null &amp;&amp;
	/// socket != null &amp;&amp; userService != null &amp;&amp; databaseService != null. &amp;&amp; virtualMachineService != null. &amp;&amp; driveService != null.<br/>
	/// Postcondition: Messaging service fully initialized and connected to the client.
	/// </remarks>
	public ClientConnection(ILogger logger, WebSocket socket, DatabaseService databaseService, UserService userService, 
		VirtualMachineService virtualMachineService, DriveService driveService, AccountService accountService)
		: base(true)
	{
		_logger = logger;
		_databaseService = databaseService;
		_userService = userService;
		_virtualMachineService = virtualMachineService;
		_driveService = driveService;
		_accountService = accountService;
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
	protected override async Task ProcessRequestAsync(IMessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result = ExitCode.Success;
		switch (request)
		{
			case MessageRequestCheckUsername reqCheckUsername:
			{
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(reqCheckUsername.Username.Trim());
				SendResponse(new MessageResponseCheckUsername(reqCheckUsername.Id, usernameAvailable));
				_logger.Verbose("Connection {Id} made check username.", ClientId);
				break;
			}

			case MessageRequestCreateAccount reqCreateAccount:
			{
				if (!Common.IsValidUsername(reqCreateAccount.Username))
				{
					SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id, MessageResponseCreateAccount.Status.InvalidUsernameSyntax));
					break;
				}

				if (!Common.IsValidEmail(reqCreateAccount.Email))
				{
					SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id, MessageResponseCreateAccount.Status.InvalidEmail));
					break;
				}

				if (Common.PasswordStrength(reqCreateAccount.Password) < 5)
				{
					SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id, MessageResponseCreateAccount.Status.Failure));
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
						SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id,
							MessageResponseCreateAccount.Status.Failure));
					else
					{
						_userService.LoginAsUser(this, ActualUser.Id);
						SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id, MessageResponseCreateAccount.Status.Success, ActualUser));
						_logger.Information("Created account {Username}, {Email}", usernameTrimmed, emailTrimmed);
					}
					
					break;
				}
				if (result == ExitCode.UserAlreadyExists)
					status = MessageResponseCreateAccount.Status.UsernameNotAvailable;
				else
					status = MessageResponseCreateAccount.Status.Failure;
				
				SendResponse(new MessageResponseCreateAccount(reqCreateAccount.Id, status));
				_logger.Warning("Account creation failed. Error {Error}.", result);
				break;
			}

			case MessageRequestDeleteAccount reqDeleteAccount:
			{
				if (!IsLoggedIn)
				{
					SendResponse(new MessageResponseDeleteAccount(reqDeleteAccount.Id, false));
					break;
				}
				
				if (reqDeleteAccount.UserId == ActionUser!.Id)
				{
					if (IsLoggedInAsSubUser && !HasPermission(UserPermissions.UserDelete))
					{
						SendResponse(new MessageResponseDeleteAccount(reqDeleteAccount.Id, false));
						_logger.Warning("User {Username} attempted account deletion of user {UserId}. Permission denied.", ActualUser!.Username, reqDeleteAccount.UserId);
						break;
					}
				}
				else
				{
					User? user = await _databaseService.GetUserAsync(reqDeleteAccount.UserId);
					if (user is not SubUser subUser || subUser.OwnerId != ActualUser!.Id 
					                                || !subUser.OwnerPermissions.HasPermission(UserPermissions.UserDelete.AddIncluded()))
					{
						SendResponse(new MessageResponseDeleteAccount(reqDeleteAccount.Id, false));
						_logger.Warning("User {Username} attempted account deletion of user {UserId}. Permission denied.", ActualUser!.Username, reqDeleteAccount.UserId);
						break;
					}
				}

				result = await _accountService.DeleteAccountAsync(reqDeleteAccount.UserId);
				
				SendResponse(new MessageResponseDeleteAccount(reqDeleteAccount.Id, result == ExitCode.Success));
				
				if (result == ExitCode.Success)
					_logger.Information("User {Username} deleted {TargetId}.", ActualUser!.Username, reqDeleteAccount.UserId);
				else
					_logger.Information("Failed deleting user with ID {UserId}. By {User}.", reqDeleteAccount.UserId,  ActualUser!.Username);
				
				break;
			}

			case MessageRequestLogin reqLogin:
			{
				string usernameTrimmed = reqLogin.Username.Trim();
				User? user = await _databaseService.GetUserAsync(usernameTrimmed);
				if (user == null)
				{
					_logger.Warning("Login attempt on non-existing user {Username}.", usernameTrimmed);
					SendResponse(new MessageResponseLogin(reqLogin.Id));
					break;	
				}

				TimeSpan? loginBlock = await _databaseService.CanUserLoginAsync(user.Id);
				if (loginBlock.HasValue)
				{
					if (loginBlock.Value == TimeSpan.MaxValue)
						SendResponse(new MessageResponseLogin(reqLogin.Id));
					
					else 
						SendResponse(new MessageResponseLogin(reqLogin.Id, loginBlock.Value));
				
					_logger.Information("User {User} attempted login while blocked. Block time left: {Time}", usernameTrimmed, loginBlock.Value);
					break;
				}
				
				result = await _userService.LoginAsync(usernameTrimmed, reqLogin.Password, this);
				if (result != ExitCode.Success)
				{
					bool blocked = await _databaseService.UserBadLoginAsync(user.Id);

					if (blocked)
					{
						SendResponse(new MessageResponseLogin(reqLogin.Id, SharedDefinitions.BadLoginBlock));
						_logger.Information("User {User} blocked from login for {Time} time due to multiple failed login attempts.", usernameTrimmed, SharedDefinitions.BadLoginBlock);
					}

					else
					{
						SendResponse(new MessageResponseLogin(reqLogin.Id));
						_logger.Information("Failed login attempt on user {Username}.", usernameTrimmed);
					}
					
					break;
				}

				User = null;
				ActualUser = user;

				SendResponse(new MessageResponseLogin(reqLogin.Id, ActualUser));
				
				await _databaseService.UserGoodLoginAsync(ActualUser.Id);
				_logger.Information("User {Username} has successfully logged in.", usernameTrimmed);
				break;
			}

			case MessageRequestLogout reqLogout:
			{
				if (IsLoggedIn)
				{
					_userService.Logout(this);

					if (IsLoggedInAsSubUser)
					{
						_logger.Information("User {Actual} successfully logged out from sub-user {SubUser}.", ActualUser!.Username, User!.Username);
						User = null;
					}
					else
					{
						_logger.Information("User {Actual} successfully logged out.", ActualUser!.Username);
						ActualUser = null;
					}
					
					SendResponse(new MessageResponseLogout(reqLogout.Id, MessageResponseLogout.Status.Success, ActualUser!));
				}
				else
				{
					_logger.Warning("Logout attempt from user when client not logged in.");
					SendResponse(new MessageResponseLogout(reqLogout.Id, MessageResponseLogout.Status.UserNotLoggedIn));
				}

				break;
			}

			case MessageRequestLoginSubUser reqLoginSubUser:
			{
				User? user = await _databaseService.GetUserAsync(reqLoginSubUser.UserId);
				if (!IsLoggedIn || IsLoggedInAsSubUser || user is not SubUser subUser || subUser.OwnerId != ActualUser!.Id)
				{
					SendResponse(new MessageResponseLoginSubUser(reqLoginSubUser.Id));
					_logger.Warning("Login into sub-user failed attempt. Permission denied. Attempted login into {UserId}.", reqLoginSubUser.UserId);
					break;
				}

				User = subUser;
				
				_userService.LoginToSubUser(this);
				
				SendResponse(new MessageResponseLoginSubUser(reqLoginSubUser.Id, subUser));
				_logger.Information("User {Actual} successfully logged into sub-user {SubUser}.", ActualUser.Username, user.Username);
				break;
			}

			case MessageRequestCreateSubUser reqCreateSubUser:
			{
				if (!IsLoggedIn || IsLoggedInAsSubUser || Common.PasswordStrength(reqCreateSubUser.Password) < 5)
				{
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Failure));
					_logger.Warning("Attempted create sub-user on invalid condition. User/client {User}.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				if (!Common.IsValidUsername(reqCreateSubUser.Username))
				{
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.InvalidUsernameSyntax));
					_logger.Warning("Attempted create sub-user with invalid username syntax. User/client {User}.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				if (!Common.IsValidEmail(reqCreateSubUser.Email))
				{
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.InvalidEmail));
					_logger.Warning("Attempted create sub-user with invalid email syntax. User/client {User}.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				result = await _databaseService.RegisterUserAsync(ActionUser!.Id, reqCreateSubUser.Permissions,
					reqCreateSubUser.Username, reqCreateSubUser.Email, reqCreateSubUser.Password);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Success));

					User? user = await _databaseService.GetUserAsync(reqCreateSubUser.Username);
					if (user is SubUser subUser)
						_userService.NotifySubUserCreated(subUser);
					
					_logger.Information("Created sub-user \"{SubUser}\" - \"{Email}\" under {Username}.", reqCreateSubUser.Username, reqCreateSubUser.Email, ActualUser!.Username);
					break;
				}
				if (result == ExitCode.UserAlreadyExists)
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.UsernameNotAvailable));
				else
					SendResponse(new MessageResponseCreateSubUser(reqCreateSubUser.Id, MessageResponseCreateSubUser.Status.Failure));
				
				_logger.Information("Failed to create sub-user with username {Username} email {Email}. Error {Error}. By user {User}.", reqCreateSubUser.Username, reqCreateSubUser.Email, result, ActualUser!.Username);
				break;
			}

			case MessageRequestSetOwnerPermissions reqSetOwnerPermissions:
			{
				if (!IsLoggedIn || reqSetOwnerPermissions.Permissions.AddIncluded() != reqSetOwnerPermissions.Permissions)
				{
					SendResponse(new MessageResponseSetOwnerPermissions(reqSetOwnerPermissions.Id, false));
					_logger.Warning("Set owner permissions on user {UserId} failed, Permission denied. By user/client {User}.", reqSetOwnerPermissions.UserId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				int ownerId;
				if (reqSetOwnerPermissions.UserId == ActualUser!.Id)
				{
					/* Can only grant the owner more permissions. */
					if (ActualUser is not SubUser subUser 
					    || (subUser.OwnerPermissions & reqSetOwnerPermissions.Permissions) != subUser.OwnerPermissions)
					{
						SendResponse(new MessageResponseSetOwnerPermissions(reqSetOwnerPermissions.Id, false));
						_logger.Warning("Removing owner permissions on user {UserId} failed, Permission denied.", reqSetOwnerPermissions.UserId);
						break;
					}
					
					ownerId = subUser.OwnerId;
				}
				else 
				{
					/* Owner can only remove his permissions over his sub-users. */
					User? user = await _databaseService.GetUserAsync(reqSetOwnerPermissions.UserId);
					
					if (user is not SubUser subUser || subUser.OwnerId != ActualUser!.Id
					    || (reqSetOwnerPermissions.Permissions & subUser.OwnerPermissions) != reqSetOwnerPermissions.Permissions)
					{
						SendResponse(new MessageResponseSetOwnerPermissions(reqSetOwnerPermissions.Id, false));
						_logger.Warning("Grant owner permissions on user {UserId} failed, Permission denied.", reqSetOwnerPermissions.UserId);
						break;					
					}

					ownerId = subUser.OwnerId;
				}

				result = await _databaseService.UpdateOwnerPermissionsAsync(reqSetOwnerPermissions.UserId, reqSetOwnerPermissions.Permissions);
				
				SendResponse(new MessageResponseSetOwnerPermissions(reqSetOwnerPermissions.Id, result == ExitCode.Success));

				if (result == ExitCode.Success)
				{
					User? user = await _databaseService.GetUserAsync(reqSetOwnerPermissions.UserId);
					if (user is not SubUser subUser)
						break;

					if (IsLoggedInAsSubUser && subUser.Id == User!.Id)
						User = subUser;
					
					else if (!IsLoggedInAsSubUser && subUser.Id == ActualUser!.Id)
						ActualUser = subUser;

					_userService.NotifyUserDataChanged(user);

					_logger.Information("Set owner permissions successfull. Set on user {Target} by {Actual}.", user.Username, ActualUser.Username);
				}
				else
					_logger.Warning("Set owner permissions on user ID {UserId} has failed. Error {Error}. By user {User}.", reqSetOwnerPermissions.UserId, result, ActualUser!.Username);
				
				break;
			}

			case MessageRequestResetPassword reqResetPassword:
			{
				if (!IsLoggedIn || IsLoggedInAsSubUser || Common.PasswordStrength(reqResetPassword.NewPassword) < 5)
				{
					SendResponse(new MessageResponseResetPassword(reqResetPassword.Id, MessageResponseResetPassword.Status.Failure));
					_logger.Warning("Reset password on user {UserId} failed, permission denied.", ActualUser?.Username ?? string.Empty);
					break;
				}

				if (!await _databaseService.IsValidLoginAsync(ActionUser!.Username, reqResetPassword.Password))
				{
					SendResponse(new MessageResponseResetPassword(reqResetPassword.Id, MessageResponseResetPassword.Status.InvalidPassword));
					_logger.Warning("Reset password on user {UserId} failed, invalid password.", ActualUser!.Username);
					break;
				}

				result = await _databaseService.ResetUserPasswordAsync(ActionUser.Id, reqResetPassword.NewPassword);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseResetPassword(reqResetPassword.Id, MessageResponseResetPassword.Status.Success));
					_logger.Information("Password reset successfully on user {User}.", ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseResetPassword(reqResetPassword.Id, MessageResponseResetPassword.Status.Failure));
					_logger.Information("Password reset on user {User} has failed. Error {Error}", ActualUser!.Username, result);
				}
				
				break;
			}

			case MessageRequestListSubUsers reqListSubUsers:
			{
				if (!IsLoggedIn)
				{
					SendResponse(new MessageResponseListSubUsers(reqListSubUsers.Id, MessageResponseListSubUsers.Status.Failure));
					_logger.Information("Attempted list sub-users by not logged in client. Connection ID {Id}", ClientId);
					break;
				}

				SubUser[]? subUsers = await _databaseService.GetSubUsersAsync(ActionUser!.Id);
				if (subUsers == null)
				{
					SendResponse(new MessageResponseListSubUsers(reqListSubUsers.Id, MessageResponseListSubUsers.Status.Failure));
					_logger.Warning("List sub-users of user {Action} by {Actual} has failed.", ActionUser.Username, ActualUser!.Username);
					break;				
				}
				
				SendResponse(new MessageResponseListSubUsers(reqListSubUsers.Id, MessageResponseListSubUsers.Status.Success, subUsers));
				_logger.Information("Listed sub-users of {Action} by {Actual} successfully.", ActionUser.Username, ActualUser!.Username);
				break;
			}

			case MessageRequestCreateVm reqCreateVm:
			{
				if (!HasPermission(UserPermissions.VirtualMachineCreate))
				{
					SendResponse(new MessageResponseCreateVm(reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					_logger.Warning("Attempt to create VM by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
			
				string vmNameTrimmed = reqCreateVm.Name.Trim();
				result = await _virtualMachineService.CreateVirtualMachineAsync(ActionUser!.Id, vmNameTrimmed,
					reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, reqCreateVm.RamSizeMiB, reqCreateVm.BootMode);
				
				if (result == ExitCode.VmAlreadyExists)
				{
					SendResponse(new MessageResponseCreateVm(reqCreateVm.Id, MessageResponseCreateVm.Status.VmAlreadyExists));
					_logger.Information("VM creation by user {User} has failed. VM already exists.", ActualUser!.Username);
					break;
				}

				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateVm(reqCreateVm.Id, MessageResponseCreateVm.Status.Failure));
					_logger.Information("VM creation by user {User} has failed. Error {Error}.", ActualUser!.Username, result);
					break;				
				}

				int id = await _databaseService.GetVmIdAsync(ActionUser.Id, vmNameTrimmed);		/* Must be valid because we just successfully created the VM */
				SendResponse(new MessageResponseCreateVm(reqCreateVm.Id, MessageResponseCreateVm.Status.Success, id));
				
				await _userService.NotifyVirtualMachineCreatedAsync(
					new VmGeneralDescriptor(id, vmNameTrimmed, reqCreateVm.OperatingSystem, reqCreateVm.CpuArchitecture, 
						VmState.ShutDown, reqCreateVm.RamSizeMiB, reqCreateVm.BootMode
					)
				);
				
				_logger.Information("Successfully created VM by user {User}.", ActualUser!.Username);
				break;
			}

			case MessageRequestDeleteVm reqDeleteVm:
			{
				if (!HasPermission(UserPermissions.VirtualMachineDelete) || await _databaseService.GetVmOwnerIdAsync(reqDeleteVm.VmId) != ActionUser!.Id)
				{
					SendResponse(new MessageResponseDeleteVm(reqDeleteVm.Id, MessageResponseDeleteVm.Status.Failure));
					_logger.Warning("Attempt to delete VM {Id} by user/client {ActualUser} has failed, permission denied.", reqDeleteVm.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				if (await _virtualMachineService.GetVmStateAsync(reqDeleteVm.VmId) != VmState.ShutDown)
				{
					SendResponse(new MessageResponseDeleteVm(reqDeleteVm.Id, MessageResponseDeleteVm.Status.VirtualMachineIsRunning));
					_logger.Information("User {Actual} attempted deletion of a running VM. ({Id})", ActualUser!.Username, reqDeleteVm.VmId);
					break;					
				}

				if (await _databaseService.IsVmExistsAsync(reqDeleteVm.VmId))
				{
					SendResponse(new MessageResponseDeleteVm(reqDeleteVm.Id, MessageResponseDeleteVm.Status.Success));
					
					/* First notifying of VM deletion and then deleting the VM, because this function depends on the VM being in the database. */
					await _userService.NotifyVirtualMachineDeletedAsync(reqDeleteVm.VmId);
				
					/* Must succeed because the VM exists. */
					await _databaseService.DeleteVmAsync(reqDeleteVm.VmId);
					_logger.Information("Successfully deleted VM {VmId}. By user {User}.", reqDeleteVm.VmId, ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseDeleteVm(reqDeleteVm.Id, MessageResponseDeleteVm.Status.Failure));
					_logger.Warning("Failed deleting VM {VmId}. User {User}.", reqDeleteVm.VmId, ActualUser!.Username);
				}
				
				break;
			}

			case MessageRequestListVms reqListVms:
			{
				if (!HasPermission(UserPermissions.VirtualMachineList))
				{
					SendResponse(new MessageResponseListVms(reqListVms.Id, MessageResponseListVms.Status.Failure));
					_logger.Warning("Attempt to list VMs by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				VmGeneralDescriptor[]? vms = await _databaseService.GetVmGeneralDescriptorsOfUserAsync(ActionUser!.Id);
				if (vms == null)
				{
					SendResponse(new MessageResponseListVms(reqListVms.Id, MessageResponseListVms.Status.Failure));
					_logger.Warning("Could not fetch VMs of user {User}.", ActionUser!.Username);
					break;
				}
				
				SendResponse(new MessageResponseListVms(reqListVms.Id, MessageResponseListVms.Status.Success, vms));
				_logger.Information("List VMs successfully. User {ActualUser}", ActualUser!.Username);
				
				break;
			}

			case MessageRequestCheckVmExist reqCheckVmExist:
			{
				if (!HasPermission(UserPermissions.VirtualMachineList))
				{
					SendResponse(new MessageResponseCheckVmExist(reqCheckVmExist.Id, false));
					_logger.Warning("Attempt to check VM ({VM}) exists by user/client {ActualUser} has failed, permission denied.", reqCheckVmExist.Name, ActualUser?.Username ?? ClientId.ToString());
					break;
				}
			
				SendResponse(new MessageResponseCheckVmExist(reqCheckVmExist.Id, 
					IsLoggedIn && await _virtualMachineService.IsVmExistsAsync(ActionUser!.Id, reqCheckVmExist.Name.Trim()))
				);
				
				_logger.Information("Check VM exists done successfully. By user {User}.", ActualUser!.Username);
				break;
			}

			case MessageRequestVmStartup reqVmStartup:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmStartup.VmId))
				{
					SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
					_logger.Warning("Attempt to power on VM {VM} by user/client {ActualUser} has failed, permission denied.", reqVmStartup.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				result = await _virtualMachineService.PowerOnVirtualMachineAsync(reqVmStartup.VmId);
				if (result == ExitCode.Success)
				{
					VmGeneralDescriptor? descriptor = await _databaseService.GetVmGeneralDescriptorAsync(reqVmStartup.VmId);
					if (descriptor == null)
					{
						SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
						await _virtualMachineService.PowerOffAndDestroyOnTimeoutAsync(reqVmStartup.VmId);
						_logger.Warning("VM {VM} descriptor fetch failed.", reqVmStartup.VmId);
						break;
					}
					
					SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.Success));
					_logger.Information("VM {VM} powered on successfully. By user {User}.", reqVmStartup.VmId, ActualUser!.Username);
					break;
				} 
				if (result == ExitCode.VmAlreadyRunning)
					SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.VmAlreadyRunning));
				
				else if (result == ExitCode.InsufficientMemory)
					SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.ServerStarvation));
				
				else
					SendResponse(new MessageResponseVmStartup(reqVmStartup.Id, MessageResponseVmStartup.Status.Failure));
			
				_logger.Information("Failed power on VM {VM}. By user {User}. Error {Error}.", reqVmStartup.VmId, ActualUser!.Username, result);
				
				break;
			}

			case MessageRequestVmShutdown reqVmShutdown:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmShutdown.VmId))
				{
					SendResponse(new MessageResponseVmShutdown(reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
					_logger.Warning("Attempt to power off VM {VM} by user/client {ActualUser} has failed, permission denied.", reqVmShutdown.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				Task<ExitCode> task = _virtualMachineService.PowerOffVirtualMachineAsync(reqVmShutdown.VmId);
				if (task.IsCompleted)
				{
					if (task.Result == ExitCode.Success)
						SendResponse(new MessageResponseVmShutdown(reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
					
					else if (task.Result == ExitCode.VmIsShutDown)
						SendResponse(new MessageResponseVmShutdown(reqVmShutdown.Id, MessageResponseVmShutdown.Status.VmIsShutDown));
					
					else
						SendResponse(new MessageResponseVmShutdown(reqVmShutdown.Id, MessageResponseVmShutdown.Status.Failure));
				}
				else
					SendResponse(new MessageResponseVmShutdown(reqVmShutdown.Id, MessageResponseVmShutdown.Status.Success));
		
				_logger.Information("Sent shutdown signal to VM {VM}. By user {User}.", reqVmShutdown.VmId, ActualUser!.Username);
				break;
			}

			case MessageRequestVmForceOff reqVmForceOff:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineUse, reqVmForceOff.VmId))
				{
					SendResponse(new MessageResponseVmForceOff(reqVmForceOff.Id, MessageResponseVmForceOff.Status.Failure));
					_logger.Warning("Attempt to force off VM {VM} by user/client {ActualUser} has failed, permission denied.", reqVmForceOff.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				result = _virtualMachineService.ForceOffVirtualMachine(reqVmForceOff.VmId);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseVmForceOff(reqVmForceOff.Id, MessageResponseVmForceOff.Status.Success));
					_logger.Information("Successfully forced off VM {VM}. By user {User}", reqVmForceOff.VmId, ActualUser!.Username);
					break;
				}
			
				if (result == ExitCode.VmIsShutDown)
					SendResponse(new MessageResponseVmForceOff(reqVmForceOff.Id, MessageResponseVmForceOff.Status.VmIsShutDown));
				
				else
					SendResponse(new MessageResponseVmForceOff(reqVmForceOff.Id, MessageResponseVmForceOff.Status.Failure));
			
				_logger.Information("Failed force off VM {VM} by user {User}. Error {Error}.", reqVmForceOff.VmId, ActualUser!.Username, result);
				
				break;
			}

			case MessageRequestVmStreamStart reqVmStreamStart:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineWatch, reqVmStreamStart.VmId))
				{
					SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id, MessageResponseVmStreamStart.Status.Failure));
					_logger.Warning("Attempt to start stream of VM {VM} by user/client {ActualUser} has failed, permission denied.", reqVmStreamStart.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				if (_streamVmId != -1)
				{
					if (_streamVmId == reqVmStreamStart.VmId)
					{
						PixelFormat? pixelsFmt = _virtualMachineService.GetScreenStreamPixelFormat(reqVmStreamStart.VmId);
						if (pixelsFmt != null)
						{
							SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id, 
								MessageResponseVmStreamStart.Status.AlreadyStreaming, pixelsFmt));
						
							_virtualMachineService.EnqueueGetFullFrame(reqVmStreamStart.VmId);
						}
						else
						{
							SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id,
								MessageResponseVmStreamStart.Status.Failure));
							
							_logger.Warning("Could not get VM {VM} stream pixel format. By user {User}.", reqVmStreamStart.VmId, ActualUser!.Username);
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
					SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id, 
						MessageResponseVmStreamStart.Status.Failure));
					_logger.Warning("Could not subscribe to VM {VM} screen stream. By user {User}.", reqVmStreamStart.VmId, ActualUser!.Username);
					break;
				}
				
				/* Audio not supported on web and android. */
				if (IsUdpMessagingRunning)
				{
					result = _virtualMachineService.SubscribeToVmAudioPacketReceived(reqVmStreamStart.VmId, OnVmNewAudioPacket);

					if (result != ExitCode.Success)
					{
						SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id, MessageResponseVmStreamStart.Status.Failure));
						_logger.Warning("Could not subscribe to VM {VM} audio stream. By user {User}.", reqVmStreamStart.VmId, ActualUser!.Username);

						_virtualMachineService.UnsubscribeFromVmNewFrameReceived(reqVmStreamStart.VmId, OnVmNewFrame);
						break;
					}
				}
				
				PixelFormat pixelFormat = _virtualMachineService.GetScreenStreamPixelFormat(reqVmStreamStart.VmId)!;

				SendResponse(new MessageResponseVmStreamStart(reqVmStreamStart.Id,
					MessageResponseVmStreamStart.Status.Success, pixelFormat));
					
				_streamVmId = reqVmStreamStart.VmId;
				_virtualMachineService.EnqueueGetFullFrame(reqVmStreamStart.VmId);
				_logger.Information("Successfully started VM {VM} stream. By user {User}.", reqVmStreamStart.VmId, ActualUser!.Username);
				break;
			}

			case MessageRequestVmStreamStop reqVmStreamStop:
			{
				if (!await HasPermissionVmOwnerAsync(UserPermissions.VirtualMachineWatch, reqVmStreamStop.VmId))
				{
					SendResponse(new MessageResponseVmStreamStop(reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Failure));
					_logger.Warning("Attempt to stop stream of VM {VM} by user/client {ActualUser} has failed, permission denied.", reqVmStreamStop.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				if (_streamVmId == -1)
				{
					SendResponse(new MessageResponseVmStreamStop(reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.StreamNotRunning));
					_logger.Warning("Attempt to stop stream of VM {VM} by user {ActualUser} has failed, stream not running.", reqVmStreamStop.VmId, ActualUser!.Username);
					break;
				}
				
				result = _virtualMachineService.UnsubscribeFromVmNewFrameReceived(reqVmStreamStop.VmId, OnVmNewFrame);
				_virtualMachineService.UnsubscribeFromVmAudioPacketReceived(reqVmStreamStop.VmId, OnVmNewAudioPacket);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseVmStreamStop(reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Success));
					_streamVmId = -1;
					_logger.Information("Successfully stopped stream of VM {VM}. By user {ActualUser}.", reqVmStreamStop.VmId, ActualUser!.Username);
				} 
				else if (result == ExitCode.VmScreenStreamNotRunning)	/* Should not happen. Doing it for safety. */
				{
					SendResponse(new MessageResponseVmStreamStop(reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.StreamNotRunning));
					_streamVmId = -1;
					_logger.Warning("Attempt to stop stream of VM {VM} by user {ActualUser} has failed, stream not running.", reqVmStreamStop.VmId, ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseVmStreamStop(reqVmStreamStop.Id, MessageResponseVmStreamStop.Status.Failure));
					_logger.Warning("Attempt to stop stream of VM {VM} by user {ActualUser} has failed, error {Error}", reqVmStreamStop.VmId, ActualUser!.Username, result);
				}
				break;
			}
			
			case MessageRequestCreateDriveOs reqCreateDrive:
			{
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveOs(reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Failure));
					_logger.Warning("Attempt to create OS drive by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				string driveNameTrimmed = reqCreateDrive.Name.Trim();

				result = await _driveService.CreateOperatingSystemDriveAsync(ActionUser!.Id, driveNameTrimmed, reqCreateDrive.OperatingSystem, reqCreateDrive.SizeMiB);
				
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDriveOs( reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.DriveAlreadyExists));
					_logger.Information("Attempt to create OS drive by user {User} failed, drive already exists.", ActualUser!.Username);
					break;			
				}
				if (result == ExitCode.Success)
				{
					/* Must succeed because the drive was created successfully */
					int driveId = await _driveService.GetDriveIdAsync(ActionUser.Id, driveNameTrimmed);		
					SendResponse(new MessageResponseCreateDriveOs(reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Success, driveId));
					
					await _userService.NotifyDriveCreatedAsync(
						new DriveGeneralDescriptor(
							driveId, 
							driveNameTrimmed, 
							reqCreateDrive.SizeMiB, 
							_driveService.GetDriveSectorSize(driveId),
							reqCreateDrive.OperatingSystem == OperatingSystem.MiniCoffeeOS 
								? DriveType.Floppy 
								: DriveType.Disk,
							_driveService.GetDrivePartitionTableType(driveId)
						)
					);
				
					_logger.Information("Successfully created OS drive. By user {User}.", ActualUser!.Username);
					break;				
				}

				SendResponse(new MessageResponseCreateDriveOs(reqCreateDrive.Id, MessageResponseCreateDriveOs.Status.Failure));
				_logger.Warning("Failed creating OS drive, by user {User}. Error {Error}.", ActualUser!.Username, result);
				break;
			}

			case MessageRequestCreateDriveFs reqCreateDriveFs:
			{
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveFs(reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Failure));
					_logger.Warning("Attempt to create filesystem drive by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				string driveName = reqCreateDriveFs.Name.Trim();
				result = await _driveService.CreateFileSystemDriveAsync(ActionUser!.Id, driveName, reqCreateDriveFs.SizeMb, reqCreateDriveFs.FileSystem);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDriveFs(reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Success));
					
					DriveGeneralDescriptor descriptor = (await _driveService.GetDriveGeneralDescriptorAsync(ActionUser.Id, driveName))!;
					await _userService.NotifyDriveCreatedAsync(descriptor);
					_logger.Information("Successfully created filesystem drive. By user {User}.", ActualUser!.Username);
					break;
				}
				if (result == ExitCode.DriveAlreadyExists)
					SendResponse(new MessageResponseCreateDriveFs(reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.DriveAlreadyExists));
				else
					SendResponse(new MessageResponseCreateDriveFs(reqCreateDriveFs.Id, MessageResponseCreateDriveFs.Status.Failure));
				
				_logger.Warning("Failed creating filesystem drive, by user {User}. Error {Error}.", ActualUser!.Username, result);
				break;
			}

			case MessageRequestCreateDriveFromImage reqCreateDrive:
			{
				if (!HasPermission(UserPermissions.DriveCreate))
				{
					SendResponse(new MessageResponseCreateDriveFromImage(reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.Failure));
					_logger.Warning("Attempt to create drive by disk image by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				string name = reqCreateDrive.Name.Trim();
				result = await _databaseService.CreateDriveAsync(ActionUser!.Id, name, (int)(reqCreateDrive.Size / 1024UL / 1024UL), reqCreateDrive.Type);
				if (result == ExitCode.DriveAlreadyExists)
				{
					SendResponse(new MessageResponseCreateDriveFromImage(reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.DriveAlreadyExists));
					_logger.Information("Failed creating drive from disk image. By user {User}. Drive already exists.", ActualUser!.Username);
					break;
				}
				if (result != ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDriveFromImage(reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.DriveAlreadyExists));
					_logger.Warning("Failed creating drive from disk image. By user {User}. Error {Error}.", ActualUser!.Username, result);
					break;								
				}

				int driveId = await _databaseService.GetDriveIdAsync(ActionUser.Id, name);
				DownloadHandler handler = new DownloadHandler(reqCreateDrive.Size, _driveService.GetDriveFilePath(driveId));
				Guid transferId = CreateTransferId();
				handler.Start(transferId);
				AddTransfer(handler);
				
				SendResponse(new MessageResponseCreateDriveFromImage(reqCreateDrive.Id, MessageResponseCreateDriveFromImage.Status.Success, transferId));

				await handler.Task;

				if (handler.HasSucceeded)
				{
					DriveGeneralDescriptor? descriptor = await _driveService.GetDriveGeneralDescriptorAsync(ActionUser.Id, name);
					if (descriptor != null)
					{
						await _userService.NotifyDriveCreatedAsync(descriptor);
						_logger.Information("Successfully created disk image drive. By user {User}.", ActualUser!.Username);
					}

					else
						_logger.Warning("Failed fetching drive descriptor after creation by disk image. By user {User}.", ActualUser!.Username);
				}
				else
				{
					await _driveService.DeleteDriveAsync(driveId);
					_logger.Warning("Failed downloading drive disk image. By user {User}.", ActualUser!.Username);
				}
				
				break;
			}

			case MessageRequestConnectDrive reqConnectDrive:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveConnect, reqConnectDrive.DriveId))
				{
					SendResponse(new MessageResponseConnectDrive(reqConnectDrive.Id, MessageResponseConnectDrive.Status.Failure));
					_logger.Warning("Attempt to connect drive {Drive} to VM {VM} by user/client {ActualUser} has failed, permission denied.", reqConnectDrive.DriveId, reqConnectDrive.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				result = await _driveService.ConnectDriveAsync(reqConnectDrive.DriveId, reqConnectDrive.VmId);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseConnectDrive(reqConnectDrive.Id, MessageResponseConnectDrive.Status.Success));
					await _userService.NotifyDriveConnected(reqConnectDrive.DriveId, reqConnectDrive.VmId);
					_logger.Information("Successfully connected drive {Drive} to VM {VM}. By user {User}.", reqConnectDrive.DriveId, reqConnectDrive.VmId, ActualUser!.Username);
				}
				else if (result == ExitCode.DriveConnectionAlreadyExists)
				{
					SendResponse(new MessageResponseConnectDrive(reqConnectDrive.Id, MessageResponseConnectDrive.Status.AlreadyConnected));
					_logger.Warning("Failed connecting drive {Drive} to VM {VM}. By user {User}. Drive already connected.", reqConnectDrive.DriveId, reqConnectDrive.VmId, ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseConnectDrive(reqConnectDrive.Id, MessageResponseConnectDrive.Status.Failure));
					_logger.Warning("Failed connecting drive {Drive} to VM {VM}. By user {User}. Error {Error}.", reqConnectDrive.DriveId, reqConnectDrive.VmId, ActualUser!.Username, result);
				}
				
				break;
			}

			case MessageRequestDisconnectDrive reqDisconnectDrive:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveDisconnect, reqDisconnectDrive.DriveId))
				{
					SendResponse(new MessageResponseDisconnectDrive(reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Failure));
					_logger.Warning("Attempt to disconnect drive {Drive} from VM {VM} by user/client {ActualUser} has failed, permission denied.", reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				result = await _driveService.DisconnectDriveAsync(reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseDisconnectDrive(reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Success));
					await _userService.NotifyDriveDisconnectedAsync(reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId);
					_logger.Information("Successfully disconnected drive {Drive} from VM {VM}. By user {User}.", reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId, ActualUser!.Username);
				}
				else if (result == ExitCode.DriveConnectionAlreadyExists)
				{
					SendResponse(new MessageResponseDisconnectDrive(reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.NotConnected));
					_logger.Warning("Failed disconnecting drive {Drive} from VM {VM}. By user {User}. Drive not connected.", reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId, ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseDisconnectDrive(reqDisconnectDrive.Id, MessageResponseDisconnectDrive.Status.Failure));
					_logger.Warning("Failed disconnecting drive {Drive} from VM {VM}. By user {User}. Error {Error}.", reqDisconnectDrive.DriveId, reqDisconnectDrive.VmId, ActualUser!.Username, result);
				}
				
				break;
			}

			case MessageRequestListDriveConnections reqListDriveConnections:
			{
				if (!HasPermission(UserPermissions.DriveConnectionList))
				{
					SendResponse(new MessageResponseListDriveConnections(reqListDriveConnections.Id, MessageResponseListDriveConnections.Status.Failure));
					_logger.Warning("Attempt to list drive connections by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				DriveConnection[]? connections = await _databaseService.GetDriveConnectionsOfUserAsync(ActionUser!.Id);
				if (connections == null)
				{
					SendResponse(new MessageResponseListDriveConnections(reqListDriveConnections.Id, 
						MessageResponseListDriveConnections.Status.Failure));
					
					_logger.Warning("Failed fetching drive connections of user {User}. By user {Actual}.", ActionUser!.Username, ActualUser!.Username);
					break;				
				}
			
				SendResponse(new MessageResponseListDriveConnections(reqListDriveConnections.Id, 
					MessageResponseListDriveConnections.Status.Success, connections));
			
				_logger.Information("List drive connections successfully done. By user {User}.", ActualUser!.Username);
				break;
			}
			
			case MessageRequestListDrives reqListDrives:
			{
				if (!HasPermission(UserPermissions.DriveList))
				{
					SendResponse(new MessageResponseListDrives(reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					_logger.Warning("Attempt to list drives by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				DriveGeneralDescriptor[]? descriptors = await _driveService.GetDriveGeneralDescriptorsOfUserAsync(ActionUser!.Id);
				if (descriptors == null)
				{
					SendResponse(new MessageResponseListDrives(reqListDrives.Id, MessageResponseListDrives.Status.Failure));
					_logger.Warning("Failed fetching drives of user {User}. By user {Actual}.", ActionUser!.Username, ActualUser!.Username);
					break;
				}
				
				SendResponse(new MessageResponseListDrives(reqListDrives.Id, MessageResponseListDrives.Status.Success, descriptors));
				
				_logger.Information("List drives successfully done. By user {User}.", ActualUser!.Username);
				break;
			}

			case MessageRequestListPathItems reqListPathItems:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemList, reqListPathItems.DriveId))
				{
					SendResponse(new MessageResponseListPathItems(reqListPathItems.Id, MessageResponseListPathItems.Status.Failure));
					_logger.Warning("Attempt to list path items by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				PathItem[]? items = _driveService.ListItems(reqListPathItems.DriveId, reqListPathItems.Path);
				if (items == null)
				{
					SendResponse(new MessageResponseListPathItems(reqListPathItems.Id, MessageResponseListPathItems.Status.InvalidPath));
					_logger.Warning("Failed fetching path items on drive {Drive} on path \"{Path}\". By user {Actual}.", reqListPathItems.DriveId, reqListPathItems.Path, ActualUser!.Username);
				}
				else
				{
					SendResponse(new MessageResponseListPathItems(reqListPathItems.Id, items));
					_logger.Information("Successfully listed path items. By user {User}.", ActualUser!.Username);
				}
				
				break;
			}

			case MessageRequestDownloadItem reqDownloadItem:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemDownload, reqDownloadItem.DriveId))
				{
					SendResponse(new MessageResponseDownloadItem(reqDownloadItem.Id, MessageResponseDownloadItem.Status.Failure));
					_logger.Warning("Attempt to download item by user/client {ActualUser} has failed, permission denied.", ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				ItemStream? stream = _driveService.GetItemStream(reqDownloadItem.DriveId, reqDownloadItem.Path, FileAccess.Read);
				if (stream == null)
				{
					SendResponse(new MessageResponseDownloadItem(reqDownloadItem.Id, MessageResponseDownloadItem.Status.NoSuchItem));
					_logger.Warning("Failed fetching item stream of item on drive {Drive} on path \"{Path}\". By user {Actual}.", reqDownloadItem.DriveId, reqDownloadItem.Path, ActualUser!.Username);
					break;
				}
			
				Guid streamGuid = Guid.NewGuid();
				SendResponse(new MessageResponseDownloadItem(reqDownloadItem.Id, 
					MessageResponseDownloadItem.Status.Success, streamGuid, (ulong)stream.Stream.Length)
				);
				
				_logger.Information("Successfully started upload of item to user {User}.", ActualUser!.Username);

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
					SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					_logger.Warning("Attempt to upload item into drive {Drive} at path \"{Path}\" by user/client {ActualUser} has failed, permission denied.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser?.Username ?? ClientId.ToString());
					break;
				}

				string trimmedPath = reqUploadFile.Path.Trim().Trim(SharedDefinitions.DirectorySeparators);
				string[] pathParts = trimmedPath.Split(SharedDefinitions.DirectorySeparators);
				if (pathParts.Length == 0 || string.IsNullOrEmpty(pathParts[0]))
				{
					SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					_logger.Information("Failed file upload into drive {Drive} to path \"{Path}\". Invalid path syntax. By user {User}.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser!.Username);
					break;
				}
				
				string fileParentDirectory = string.Join('/', pathParts[..^1]);
				if (!await _driveService.ItemExistsAsync(reqUploadFile.DriveId, fileParentDirectory))
				{
					SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.InvalidPath));
					_logger.Information("Failed file upload into drive {Drive} to path \"{Path}\". Path does not exist. By user {User}.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser!.Username);
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
					SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.Failure));
					_logger.Warning("Failed creating item in drive {Drive} on path \"{Path}\". By user {User}.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser!.Username);
					break;
				}

				if (stream.MaxSize < reqUploadFile.Size)
				{
					SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.FileTooLarge));
					stream.Dispose();
					await _driveService.DeleteItemAsync(reqUploadFile.DriveId, reqUploadFile.Path);
					_logger.Information("Failed file upload into drive {Drive} to path \"{Path}\". File is too large. By user {User}.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser!.Username);
					break;
				}

				DownloadHandler handler = new DownloadHandler(reqUploadFile.Size, stream.Stream);
				handler.Start(Guid.NewGuid());
				AddTransfer(handler);
				
				SendResponse(new MessageResponseUploadFile(reqUploadFile.Id, MessageResponseUploadFile.Status.Success, handler.Id));

				await handler.Task;
				stream.Dispose();

				if (handler.HasFailed)
				{
					await _driveService.DeleteItemAsync(reqUploadFile.DriveId, path);
					_logger.Warning("Failed file upload into drive {Drive} to path \"{Path}\". Upload failed. By user {User}.", reqUploadFile.DriveId, reqUploadFile.Path, ActualUser!.Username);
					break;
				}

				await _userService.NotifyItemCreatedAsync(reqUploadFile.DriveId, trimmedPath);
				_logger.Information("Upload file into drive successfully done. By user {User}. ", ActualUser!.Username);
				break;
			}

			case MessageRequestCreateDirectory reqCreateDirectory:
			{
				if (!await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemCreate, reqCreateDirectory.DriveId))
				{
					SendResponse(new MessageResponseCreateDirectory(reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Failure));
					_logger.Warning("Attempt to create directory in drive {Drive} at \"{Path}\" by user/client {ActualUser} has failed, permission denied.", reqCreateDirectory.DriveId, reqCreateDirectory.Path, ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				result = _driveService.CreateDirectory(reqCreateDirectory.DriveId, reqCreateDirectory.Path);
				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseCreateDirectory(reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Success));
					await _userService.NotifyItemCreatedAsync(reqCreateDirectory.DriveId, reqCreateDirectory.Path);
					_logger.Information("Created directory successfully in drive. By user {User}.", ActualUser!.Username);
					break;
				}
				
				if (result == ExitCode.InvalidPath)
					SendResponse(new MessageResponseCreateDirectory(reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.InvalidPath));
				
				else
					SendResponse(new MessageResponseCreateDirectory(reqCreateDirectory.Id, MessageResponseCreateDirectory.Status.Failure));
				
				_logger.Information("Failed creating directory in drive {Drive} at path \"{Path}\". Error {Error}. By user {User}.", reqCreateDirectory.DriveId, reqCreateDirectory.Path, result, ActualUser!.Username);
					
				break;
			}

			case MessageRequestDeleteItem reqDeleteItem:
			{
				if ((!Common.IsPathToDrive(reqDeleteItem.Path) && !await HasPermissionDriveOwnerAsync(UserPermissions.DriveItemDelete, reqDeleteItem.DriveId))
				    || (Common.IsPathToDrive(reqDeleteItem.Path) && !await HasPermissionDriveOwnerAsync(UserPermissions.DriveDelete, reqDeleteItem.DriveId)))
				{
					SendResponse(new MessageResponseDeleteItem(reqDeleteItem.Id, MessageResponseDeleteItem.Status.Failure));
					_logger.Warning("Attempt to delete file in drive {Drive} at \"{Path}\" by user/client {ActualUser} has failed, permission denied.", reqDeleteItem.DriveId, reqDeleteItem.Path, ActualUser?.Username ?? ClientId.ToString());
					break;
				}
				
				await _userService.NotifyItemDeletedAsync(reqDeleteItem.DriveId, reqDeleteItem.Path);
				
				result = await _driveService.DeleteItemAsync(reqDeleteItem.DriveId, reqDeleteItem.Path);

				if (result == ExitCode.Success)
				{
					SendResponse(new MessageResponseDeleteItem(reqDeleteItem.Id, MessageResponseDeleteItem.Status.Success));
					_logger.Information("Deleted item successfully. By user {User}.", ActualUser!.Username);
					break;
				}
				
				if (result == ExitCode.InvalidPath)
					SendResponse(new MessageResponseDeleteItem(reqDeleteItem.Id, MessageResponseDeleteItem.Status.NoSuchItem));
				
				else
					SendResponse(new MessageResponseDeleteItem(reqDeleteItem.Id, MessageResponseDeleteItem.Status.Failure));

				_logger.Warning("Failed to delete file in drive {Drive} at \"{Path}\". Error {Error}. By user {User}.", reqDeleteItem.DriveId, reqDeleteItem.Path, result, ActualUser!.Username);
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
	protected override async Task ProcessInfoAsync(IMessage info)
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
					_logger.Information("Client at {IP} identified UDP port {Port}. Connected successfully.", remoteIp, infoIdentifyUdp.Port);
				}
				catch (Exception)
				{
					_logger.Information("Failed connecting to client UDP port. {IP}:{Port}.", remoteIp, infoIdentifyUdp.Port);
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
		SendInfo(new MessageInfoSubUserCreated(subUser));

	/// <summary>
	/// Notifies that the data of the given user has changed.
	/// </summary>
	/// <param name="user"></param>
	/// <remarks>
	/// Precondition: The data of the given user has changed. (username, owner permission, etc.) Service initialized and connected to client. user != null. <br/>
	/// Postcondition: Client is notified of the user data change.
	/// </remarks>
	public void NotifyUserDataChanged(User user) =>
		SendInfo(new MessageInfoUserData(user));
	
	/// <summary>
	/// Notifies the client that a user was deleted. (Either a sub-user, or the current user itself)
	/// </summary>
	/// <param name="userId">The ID of the user that was deleted. subUserId >= .</param>
	/// <remarks>
	/// Precondition: A user was deleted. Service initialized and connected to client. userId >= 1. <br/>
	/// Postcondition: Client is notified that a user was deleted.
	/// </remarks>
	public void NotifyUserDeleted(int userId) =>
		SendInfo(new MessageInfoUserDeleted(userId));
	
	/// <summary>
	/// Notifies the client that a virtual machine was created.
	/// </summary>
	/// <param name="descriptor">A descriptor of the new virtual machine. descriptor != null.</param>
	/// <remarks>
	/// Precondition: A new virtual machine was created. Service initialized and connected to client. descriptor != null. <br/>
	/// Postcondition: Client notified of the new virtual machine.
	/// </remarks>
	public void NotifyVirtualMachineCreated(VmGeneralDescriptor descriptor) =>
		SendInfo(new MessageInfoVmCreated(descriptor));
	
	/// <summary>
	/// Notifies the client that a virtual machine was deleted.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was deleted. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was deleted. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client notified of the virtual machine deletion event.
	/// </remarks>
	public void NotifyVirtualMachineDeleted(int vmId) =>
		SendInfo(new MessageInfoVmDeleted(vmId));

	/// <summary>
	/// Notifies the client that a virtual machine was powered on.
	/// </summary>
	/// <param name="vmId">The ID of the virtual machine that was powered on. vmId >= 1.</param>
	/// <remarks>
	/// Precondition: A virtual machine was powered on. Service initialized and connected to client. vmId >= 1. <br/>
	/// Postcondition: Client is notified that the virtual machine was powered on.
	/// </remarks>
	public void NotifyVirtualMachinePoweredOn(int vmId) =>
		SendInfo(new MessageInfoVmPoweredOn(vmId));
	
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
		SendInfo(new MessageInfoVmPoweredOff(vmId));
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
		SendInfo(new MessageInfoVmCrashed(vmId));
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
		SendInfo(new MessageInfoDriveCreated(descriptor));

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
		SendInfo(new MessageInfoItemCreated(driveId, path));
	
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
		SendInfo(new MessageInfoItemDeleted(driveId, path));

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
		SendInfo(new MessageInfoDriveConnected(driveId, vmId));

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
		SendInfo(new MessageInfoDriveDisconnected(driveId, vmId));
	
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
		
		MessageInfoVmScreenFrame frameMessage = new MessageInfoVmScreenFrame(frame.VmId, frame.Size, frame.CompressedFramebuffer);

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

		MessageInfoVmAudioPacket audioMessage = new MessageInfoVmAudioPacket(_streamVmId, packet);
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