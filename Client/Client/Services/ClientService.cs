using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Avalonia.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using DriveType = Shared.Drives.DriveType;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public event EventHandler? Reconnected;
	public event EventHandler<SubUser>? SubUserCreated;
	public event EventHandler<VmGeneralDescriptor>? VmCreated;
	public event EventHandler<int>? VmDeleted;
	public event EventHandler<MessageInfoVmScreenFrame>? VmScreenFrameReceived;
	public event EventHandler<MessageInfoVmAudioPacket>? VmAudioPacketReceived;
	public event EventHandler<int>? VmPoweredOn;
	public event EventHandler<int>? VmPoweredOff;
	public event EventHandler<int>? VmCrashed;
	public event EventHandler<DriveGeneralDescriptor>? DriveCreated;
	public event EventHandler<MessageInfoItemCreated>? ItemCreated;
	public event EventHandler<MessageInfoItemDeleted>? ItemDeleted;
	public event EventHandler<DriveConnection>? DriveConnected;
	public event EventHandler<DriveConnection>? DriveDisconnected;

	public bool IsLoggedIn => User != null;
	public User? User { get; private set; }
	
	/// <summary>
	/// Fully initializes client messaging and connects to the server.
	/// </summary>
	/// <remarks>
	/// Precondition: Service must not be initialized <br/>
	/// Postcondition: Service fully initialized and connected to the server.
	/// </remarks>
	public async Task InitializeAsync()
	{
		if (IsInitialized())
			return;

		if (System.OperatingSystem.IsBrowser())
			WebSocket = new ClientWebSocket();
		
		else
		{
			TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		}

		bool connected;
		do
		{
			await ConnectToServerAsync();
			connected = await TlsInitialize() == ExitCode.Success;
		} while (!connected);
		
		StartTcp();
		
		if (!System.OperatingSystem.IsBrowser())
			StartUdp();
	}

	/// <summary>
	/// Called when the user exits the application. (Only on desktop, as other platforms dont give a chance for cleanup)
	/// </summary>
	/// <remarks>
	/// Precondition: No specific condition. <br/>
	/// Postcondition: Service uninitialized and disconnected from the server.
	/// </remarks>
	public void OnExit()
	{
		Disconnect();
	}

	/// <summary>
	/// Checks if there exists a user with the given username.
	/// </summary>
	/// <param name="username">
	/// The username to check for. username != null.
	/// </param>
	/// <returns>
	/// True if there is no user with the given username, false if there is at least one user with the given username.
	/// </returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. username != null. <br/>
	/// Postcondition: Returns true if there is no user with the given username, false if there is at least one user with that username.
	/// </remarks>
	public async Task<bool?> IsUsernameAvailableAsync(string username)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCheckUsername(true, username));

		if (result != ExitCode.Success)
		{
			return null;
		}
		
		return ((MessageResponseCheckUsername)response!).Available;
	}

	/// <summary>
	/// Sends a request to crete a new account with the given username and password.
	/// </summary>
	/// <param name="username">
	/// The username of the new user. username != null.
	/// </param>
	/// <param name="email">
	/// The email of the new user. email != null
	/// </param>
	/// <param name="password">
	/// The password for the new user. password != null.
	/// </param>
	/// <returns>
	/// A status indicating the result of the account creation request.
	/// </returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. username != null &amp;&amp; email != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, a new account with the given username and password is created. The returned value will indicate success. <br/>
	/// On failure, the account is not created, and the returned value will indicate and error.
	/// </remarks>
	public async Task<MessageResponseCreateAccount.Status> CreateAccountAsync(string username, string email, string password)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateAccount(true, username, email, password));
		if (result != ExitCode.Success)
			return MessageResponseCreateAccount.Status.Failure;
		
		MessageResponseCreateAccount res = (MessageResponseCreateAccount)response!;
		if (res.Result == MessageResponseCreateAccount.Status.Success)
			User = res.User;
		
		return res.Result;
	}

	/// <summary>
	/// Sends a login request to the server.
	/// </summary>
	/// <param name="username">
	/// The username of the user to log in into. username != null.
	/// </param>
	/// <param name="password">
	/// The password of the user to log in into. password != null.
	/// </param>
	/// <returns>
	/// True if the login operation has succeeded, false otherwise. Returns null on operation failure (request sending failed, for example)
	/// </returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. username != null &amp;&amp; password != null. <br/>
	/// Postcondition: Returns true if login has succeeded, false otherwise. <br/>
	/// Will return null on operational failure, for example, if sending the login request has failed.
	/// </remarks>
	public async Task<bool?> LoginAsync(string username, string password)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestLogin(true, username, password));
		if (result != ExitCode.Success)
			return null;
		
		MessageResponseLogin res = (MessageResponseLogin)response!;
		if (res.Accepted)
			User = res.User;
		
		return res.Accepted;
	}

	/// <summary>
	/// Sends a Logout request. Logs-out on success.
	/// </summary>
	/// <returns>
	/// A status indicating the result of the operation. (For example, UserNotLoggedIn if the user was not logged in)
	/// </returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. <br/>
	/// Postcondition: On success, the user is considered not logged-in in the server. The returned value will indicate success. <br/>
	/// On failure, the user is considered logged-in in the server, and the returned value will indicate failure. That is except for if the returned status states
	/// that the user is not logged in, in that case the user is considered not logged-in in the server. (just like it was before calling this function)
	/// </remarks>
	public async Task<MessageResponseLogout.Status> LogoutAsync()
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestLogout(true));
		if (result != ExitCode.Success)
			return  MessageResponseLogout.Status.Failure;
		
		MessageResponseLogout resLogout = (MessageResponseLogout)response!;
		User = resLogout.User;
		
		return resLogout.Result;
	}

	/// <summary>
	/// Request login to a sub-user account.
	/// </summary>
	/// <param name="subUserId">The ID of the sub-user to log in to. subUserId >= 1.</param>
	/// <returns>True if the login attempt has succeeded, false otherwise.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, the user is logged in as itself (not as a sub-user). <br/>
	/// Postcondition: On success, true is returned and the user is logged in to the sub-user.
	/// On failure, false is returned and the user is not logged in to the sub-user.
	/// </remarks>
	public async Task<bool> LoginToSubUserAsync(int subUserId)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestLoginSubUser(true, subUserId));
		if (result != ExitCode.Success)
			return false;
		
		MessageResponseLoginSubUser res = (MessageResponseLoginSubUser)response!;
		if (res.Success)
			User = res.SubUser;
		
		return res.Success;
	}

	/// <summary>
	/// Requests to create a sub-user with the given information.
	/// </summary>
	/// <param name="username">The username for the new sub-user. Must be unique, no user with this username should exist. username != null.</param>
	/// <param name="email">The email address of the new sub-user. Must be a valid email address, and in valid email syntax. email != null.</param>
	/// <param name="password">The password of the new sub-user. password != null.</param>
	/// <param name="permissions">The permissions of the current user over the new sub-user.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. No user with the given username exist.
	/// The given email is valid and is in valid email syntax. username != null &amp;&amp; email != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, the sub-user is created and the returned status indicates success.
	/// On failure, the sub-user is not created and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseCreateSubUser.Status> CreateSubUserAsync(string username, string email, string password, UserPermissions permissions)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateSubUser(
			true, username, email, password, permissions));

		if (result != ExitCode.Success)
			return MessageResponseCreateSubUser.Status.Failure;

		return ((MessageResponseCreateSubUser)response!).Result;
	}

	/// <summary>
	/// Requests a list of all sub-users of the current user.
	/// </summary>
	/// <returns>A list of users, describing the sub-users of the current user. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. The user is logged in. <br/>
	/// Postcondition: On success, a list of users is returned, describing the sub-users of the current user.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<SubUser[]?> GetSubUsersAsync()
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestListSubUsers(true));
		if (result != ExitCode.Success)
			return null;

		MessageResponseListSubUsers res = (MessageResponseListSubUsers)response!;
		if (res.Result != MessageResponseListSubUsers.Status.Success)
			return null;
		
		return res.Users;
	}

	/// <summary>
	/// Sends a create VM request.
	/// </summary>
	/// <param name="name">The name of the new virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the new virtual machine. operatingSystem != null.</param>
	/// <param name="cpuArchitecture">The CPU architecture to be used for the virtual machine. cpuArchitecture != null.</param>
	/// <param name="ramSizeMiB">The amount of RAM storage for the virtual machine. ramSizeMiB > 0 &amp;&amp; ramSizeMiB &lt;= SharedDefinitions.VmRamSizeMbMax.</param>
	/// <param name="bootMode">The boot mode - UEFI or BIOS. bootMode != null.</param>
	/// <returns>The result of the VM creation operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User must be logged in. name != null <br/>
	/// Postcondition: On success, a new virtual machine is created and the servers response is returned. <br/>
	/// On networking failure, null is returned. On other failure, the servers response is returned.
	/// </remarks>
	public async Task<MessageResponseCreateVm?> CreateVirtualMachineAsync(string name,
		OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, int ramSizeMiB, BootMode bootMode)
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(
			new MessageRequestCreateVm(true, name, operatingSystem, cpuArchitecture, ramSizeMiB, bootMode)
		);

		if (response == null)
		{
			return null;
		}

		return (MessageResponseCreateVm)response;
	}

	/// <summary>
	/// Requests the server to delete the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to delete. id >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, a virtual machine with the given ID exists. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is deleted and the returned exit code indicates success. <br/>
	/// On failure, the virtual machine is not deleted and the returned exit code indicates the error.
	/// </remarks>
	public async Task<MessageResponseDeleteVm.Status> DeleteVirtualMachineAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDeleteVm(true, id));
		if (result != ExitCode.Success) return MessageResponseDeleteVm.Status.Failure;

		return ((MessageResponseDeleteVm)response!).Result;
	}

	/// <summary>
	/// Requests a list of all virtual machines of the user.
	/// </summary>
	/// <returns>The servers response, or null on networking failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User must be logged in. <br/>
	/// Postcondition: On success, the servers response is returned. On networking failure, null is returned.
	/// </remarks>
	public async Task<MessageResponseListVms?> GetVirtualMachinesAsync()
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(new MessageRequestListVms(true));

		if (response == null) return null;
		
		return (MessageResponseListVms)response;
	}
	
	/// <summary>
	/// Check if the user has a VM which name is the given name.
	/// </summary>
	/// <param name="name">The name of the virtual machine to check for. name != null.</param>
	/// <returns>True if the user has the VM, false otherwise or on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. The user is logged in. name != null. <br/>
	/// Postcondition: Returns whether the user has a VM which name is the given name.
	/// </remarks>
	public async Task<bool> IsVmExistsAsync(string name)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCheckVmExist(true, name));
		return result == ExitCode.Success && ((MessageResponseCheckVmExist)response!).Exists;
	}

	/// <summary>
	/// Requests to create a drive formatted with the given file system.
	/// </summary>
	/// <param name="name">The name of the new drive. Must be unique for the user. name != null.</param>
	/// <param name="sizeMb">The size of the drive to create, in MiB. Must be in valid range for the filesystem.</param>
	/// <param name="fileSystem">The file system to format the drive with. Must be a supported file system. (currently only FAT32) </param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. The given name must be unique for the user.
	/// The given drive size must be in valid range for the file system. The given file system must be supported. name != null. <br/>
	/// Postcondition: On success, the drive is created and the returned status indicates success.
	/// On failure, the drive is not created and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseCreateDriveFs.Status> CreateDriveFsAsync(string name, int sizeMb,
		FileSystemType fileSystem)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateDriveFs(true, name, sizeMb, fileSystem));
		if (result != ExitCode.Success) 
			return MessageResponseCreateDriveFs.Status.Failure;

		return ((MessageResponseCreateDriveFs)response!).Result;
	}

	/// <summary>
	/// Requests to create a drive from a disk image. Starts uploading the disk image.
	/// </summary>
	/// <param name="name">The name for the new drive. Must be unique for the user. name != null.</param>
	/// <param name="type">The type of drive to create.</param>
	/// <param name="iso">A stream of the ISO image. Used while uploading the ISO. Disposed when done. iso != null.</param>
	/// <returns>An upload handler managing the upload of the ISO image, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. The given name must be unique for the user.
	/// name != null &amp;&amp; iso != null. <br/>
	/// Postcondition: On success, the upload is started and an upload handler handling the upload is returned. On failure, null is returned.
	/// </remarks>
	public async Task<UploadHandler?> CreateDriveFromImageAsync(string name, DriveType type, Stream iso)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateDriveFromImage(true, name, type, (ulong)iso.Length));
		if (result != ExitCode.Success)
			return null;

		MessageResponseCreateDriveFromImage res = (MessageResponseCreateDriveFromImage)response!;
		if (res.Result != MessageResponseCreateDriveFromImage.Status.Success)
			return null;
		
		UploadHandler handler = new UploadHandler(this, iso);
		handler.Start(res.ImageTransferId);

		return handler;
	}
	
	/// <summary>
	/// Requests the server to create a drive with the given parameters.
	/// </summary>
	/// <param name="name">The name of the drive. Must be unique for the user. name != null.</param>
	/// <param name="size">The size of the drive in MiB. size >= 1.</param>
	/// <param name="operatingSystem">The operating system to install on the drive.</param>
	/// <returns>The servers response, or null on networking failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. name != null &amp;&amp; size >= 1.<br/>
	/// Postcondition: On success, the drive is created and the servers response is returned. <br/>
	/// On failure, if there was a networking failure, null is returned. On other failures, the servers response is returned.
	/// </remarks>
	public async Task<MessageResponseCreateDriveOs?> CreateDriveOsAsync(
		string name, int size, OperatingSystem operatingSystem)
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(new MessageRequestCreateDriveOs(true, name, size, operatingSystem));
		if (response == null)
		{
			return null;
		}
		return (MessageResponseCreateDriveOs)response;
	}

	/// <summary>
	/// Request to register a drive connection between the given drive and the given virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to connect. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to connect the drive to. vmId >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to server.
	/// The user has a drive with the given ID, and a virtual machine with the given ID. <br/>
	/// driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the drive connection is registered and the returned status states success. <br/>
	/// On failure, the drive connection is not registered and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseConnectDrive.Status> ConnectDriveAsync(int driveId, int vmId)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestConnectDrive(true, driveId, vmId));
		if (result != ExitCode.Success)
			return MessageResponseConnectDrive.Status.Failure;
		
		return ((MessageResponseConnectDrive)response!).Result;
	}

	/// <summary>
	/// Request to remove the drive connection between the given drive and the given virtual machine.
	/// </summary>
	/// <param name="driveId">The ID of the drive to disconnect. driveId >= 1.</param>
	/// <param name="vmId">The ID of the virtual machine to disconnect the drive from. vmId >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to server.
	/// The user has a drive with the given ID, and a virtual machine with the given ID. <br/>
	/// driveId >= 1 &amp;&amp; vmId >= 1. <br/>
	/// Postcondition: On success, the drive connection is removed and the returned status indicates success. <br/>
	/// On failure, the drive connection is not affected and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseDisconnectDrive.Status> DisconnectDriveAsync(int driveId, int vmId)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDisconnectDrive(true, driveId, vmId));
		if (result != ExitCode.Success)
			return MessageResponseDisconnectDrive.Status.Failure;
		
		return ((MessageResponseDisconnectDrive)response!).Result;
	}
	
	/// <summary>
	/// Requests a list of all drive connections of the user. (which drives are connected to which virtual machines)
	/// </summary>
	/// <returns>The servers response, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. <br/>
	/// Postcondition: On success, the servers response is returned. On networking failure, null is returned.
	/// </remarks>
	public async Task<MessageResponseListDriveConnections?> GetDriveConnectionsAsync()
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(new MessageRequestListDriveConnections(true));
		if (response == null) 
			return null;
		
		return (MessageResponseListDriveConnections)response;
	}
	
	/// <summary>
	/// Requests a list of all drives of the user.
	/// </summary>
	/// <returns>The servers response, or null on networking failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, user is logged in. <br/>
	/// Postcondition: On success, the servers response is returned. On networking failure, null is returned.
	/// </remarks>
	public async Task<MessageResponseListDrives?> GetDrivesAsync()
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(new MessageRequestListDrives(true));
		if (response == null) 
			return null;
		
		return (MessageResponseListDrives)response;
	}

	/// <summary>
	/// Request a list of the items under the given path in the given drive.
	/// </summary>
	/// <param name="driveId">The ID of the drive to use. driveId >= 1.</param>
	/// <param name="path">The path on the given drive under which to list the items. path != null.</param>
	/// <returns>A list of items that reside under the given path, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, user is logged in. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, a list of items representing the items under the given path in the given drive is returned.
	/// On failure, null is returned.
	/// </remarks>
	public async Task<PathItem[]?> ListItemsOnDrivePathAsync(int driveId, string path)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestListPathItems(true, driveId, path));
		if (result != ExitCode.Success) 
			return null;

		return ((MessageResponseListPathItems)response!).PathItems;
	}

	/// <summary>
	/// Requests to start a download of the given item.
	/// </summary>
	/// <param name="driveId">The drive that holds the needed item. driveId >= 1.</param>
	/// <param name="path">The path on the drive, which points to the needed item. Use an empty string for the drive's disk image. path != null.</param>
	/// <param name="destination">The destination path, where to save the item to. destination != null.</param>
	/// <returns>A download handler that handles the download, and can be used to track progress. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, user is logged in. driveId >= 1 &amp;&amp; path != null. &amp;&amp; destination != null.<br/>
	/// Postcondition: A download handler is returned, or null on failure.
	/// </remarks>
	public async Task<DownloadHandler?> StartItemDownloadAsync(int driveId, string path, string destination)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDownloadItem(true, driveId, path));
		if (result != ExitCode.Success)
			return null;

		MessageResponseDownloadItem res = (MessageResponseDownloadItem)response!;
		if (res.Result != MessageResponseDownloadItem.Status.Success)
			return null;
		
		DownloadHandler handler;
		try
		{
			handler = new DownloadHandler(res.ItemSize, destination);
		}
		catch (Exception)
		{
			return null;
		}
		
		handler.Start(res.StreamId);
		AddTransfer(handler);

		return handler;
	}

	/// <summary>
	/// Requests to start a download of the given item.
	/// </summary>
	/// <param name="driveId">The drive that holds the needed item. driveId >= 1.</param>
	/// <param name="path">The path on the drive, which points to the needed item. Use an empty string for the drive's disk image. path != null.</param>
	/// <param name="destination">The stream to write the item's content to. Must be writable. destination != null.</param>
	/// <returns>A download handler that handles the download, and can be used to track progress. Returns null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, user is logged in. driveId >= 1 &amp;&amp; path != null. &amp;&amp; destination != null. <br/>
	/// Postcondition: A download handler is returned, or null on failure.
	/// </remarks>
	public async Task<DownloadHandler?> StartItemDownloadAsync(int driveId, string path, Stream destination)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDownloadItem(true, driveId, path));
		if (result != ExitCode.Success)
			return null;

		MessageResponseDownloadItem res = (MessageResponseDownloadItem)response!;
		if (res.Result != MessageResponseDownloadItem.Status.Success)
			return null;
		
		DownloadHandler handler = new DownloadHandler(res.ItemSize, destination);
		handler.Start(res.StreamId);
		AddTransfer(handler);

		return handler;
	}

	/// <summary>
	/// Attempts to start an upload of the given file to the given drive at the given path in it.
	/// </summary>
	/// <param name="driveId">The ID of the drive to use to store the file. driveId >= 1.</param>
	/// <param name="path">The path inside the drive to store the file in. path != null.</param>
	/// <param name="source">The path to the source file, the file to write into the drive. source != null.</param>
	/// <returns>A status indicating the result of the operation, and the upload handler (which is null on failure).</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. A drive with the given ID must exist.
	/// The given path must contain the filename to save the file as. In the path parameter, the path to the file must exist, but the file itself should not exist.
	/// The source parameter must point to a valid filesystem file. driveId >= 1 &amp;&amp; path != null &amp;&amp; source != null. <br/>
	/// Postcondition: On success, the upload is started, a status of success and a valid upload handler are both returned.
	/// On failure, the upload is not started, the returned status indicates the error and the upload handler is null.
	/// </remarks>
	public async Task<(MessageResponseUploadFile.Status, UploadHandler?)> StartFileUploadAsync(int driveId, string path, string source)
	{
		ulong fileSize;
		Stream file;
		try
		{
			file = File.OpenRead(source);
		}
		catch (Exception)
		{
			return (MessageResponseUploadFile.Status.Failure, null);
		}
		
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestUploadFile(true, driveId, path, (ulong)file.Length));
		if (result != ExitCode.Success)
		{
			await file.DisposeAsync();
			return (MessageResponseUploadFile.Status.Failure, null);
		}

		MessageResponseUploadFile res = (MessageResponseUploadFile)response!;
		if (res.Result != MessageResponseUploadFile.Status.Success)
		{
			await file.DisposeAsync();
			return (res.Result, null);
		}
		
		UploadHandler handler = new UploadHandler(this, file);
		handler.Start(res.StreamId);
		AddTransfer(handler);

		return (res.Result, handler);
	}

	/// <summary>
	/// Attempts to start an upload of the given stream as a file into the given drive at the given path in it.
	/// </summary>
	/// <param name="driveId">The ID of the drive to use to store the file. driveId >= 1.</param>
	/// <param name="path">The path inside the drive to store the file in. path != null.</param>
	/// <param name="source">The stream to use as a file to upload into the drive. Must be readable. source != null.</param>
	/// <returns>A status indicating the result of the operation, and the upload handler (which is null on failure).</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. A drive with the given ID must exist.
	/// The given path must contain the filename to save the file as. In the path parameter, the path to the file must exist, but the file itself should not exist.
	/// The source parameter must be readable. driveId >= 1 &amp;&amp; path != null &amp;&amp; source != null. <br/>
	/// Postcondition: On success, the upload is started, a status of success and a valid upload handler are both returned.
	/// On failure, the upload is not started, the returned status indicates the error and the upload handler is null.
	/// </remarks>
	public async Task<(MessageResponseUploadFile.Status, UploadHandler?)> StartFileUploadAsync(int driveId, string path, Stream source)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestUploadFile(true, driveId, path, (ulong)source.Length));
		if (result != ExitCode.Success)
			return (MessageResponseUploadFile.Status.Failure, null);

		MessageResponseUploadFile res = (MessageResponseUploadFile)response!;
		if (res.Result != MessageResponseUploadFile.Status.Success)
			return (MessageResponseUploadFile.Status.Failure, null);
		
		UploadHandler handler = new UploadHandler(this, source);
		handler.Start(res.StreamId);
		AddTransfer(handler);

		return (res.Result, handler);
	}

	/// <summary>
	/// Requests to create a directory in the given drive at the given path.
	/// </summary>
	/// <param name="driveId">The ID of the drive to create the directory in. driveId >= 1.</param>
	/// <param name="path">The path inside the drive, where to create the directory. path != null.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. A drive with the given ID exists.
	/// The given path up until its last part (the directory to create) exists in the given drive. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, the directory is created and the returned status indicates success.
	/// On failure, the directory is not created and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseCreateDirectory.Status> CreateDirectoryAsync(int driveId, string path)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateDirectory(true, driveId, path));
		if (result != ExitCode.Success)
			return MessageResponseCreateDirectory.Status.Failure;

		return ((MessageResponseCreateDirectory)response!).Result;
	}
	
	/// <summary>
	/// Requests to delete the given item.
	/// </summary>
	/// <param name="driveId">The ID of the drive that holds the item to delete. driveId >= 1.</param>
	/// <param name="path">The path on the drive, points to the item to delete. Must not point to a partition. path != null.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server, user is logged in.
	/// The given path must not point to a partition. driveId >= 1 &amp;&amp; path != null. <br/>
	/// Postcondition: On success, the given item is deleted and the returned status indicates success. <br/>
	/// On failure, the item is not deleted and the returned status indicates the error.
	/// </remarks>
	public async Task<MessageResponseDeleteItem.Status> DeleteItemAsync(int driveId, string path)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDeleteItem(true, driveId, path));
		if (result != ExitCode.Success)
			return MessageResponseDeleteItem.Status.Failure;

		return ((MessageResponseDeleteItem)response!).Result;
	}

	/// <summary>
	/// Requests to power on a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to power on. id >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server.
	/// The user is logged in, and has power permissions for the VM with the given ID. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is powered on (running) and the returned status indicates success. <br/>
	/// On failure, the virtual machine is powered off and the returned exit code indicates the error.
	/// </remarks>
	public async Task<MessageResponseVmStartup.Status> PowerOnVirtualMachineAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestVmStartup(true, id));
		if (result != ExitCode.Success)
		{
			return MessageResponseVmStartup.Status.Failure;
		}
		return ((MessageResponseVmStartup)response!).Result;
	}

	/// <summary>
	/// Requests to power off a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to power off. id >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server.
	/// The user is logged in, and has power permissions for the VM with the given ID. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is powered off and the returned status indicates success. <br/>
	/// On failure, the virtual machine remains in its previous state and the returned exit code indicates the error.
	/// </remarks>
	public async Task<MessageResponseVmShutdown.Status> PowerOffVirtualMachineAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestVmShutdown(true, id));
		if (result != ExitCode.Success)
		{
			return MessageResponseVmShutdown.Status.Failure;
		}
		return ((MessageResponseVmShutdown)response!).Result;	
	}

	/// <summary>
	/// Requests to force off a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to force off. id >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server.
	/// The user is logged in, and has power permissions for the VM with the given ID. id >= 1. <br/>
	/// Postcondition: On success, the virtual machine is powered off and the returned status indicates success. <br/>
	/// On failure, the virtual machine remains in its previous state and the returned exit code indicates the error.
	/// </remarks>
	public async Task<MessageResponseVmForceOff.Status> ForceOffVirtualMachineAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestVmForceOff(true, id));
		if (result != ExitCode.Success)
		{
			return MessageResponseVmForceOff.Status.Failure;
		}
		return ((MessageResponseVmForceOff)response!).Result;	
	}

	/// <summary>
	/// Requests a video stream of the screen of the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to get a video stream of. id >= 1.</param>
	/// <returns>The servers' response. On networking failure, null is returned.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in, and has viewing permissions for the given VM. id >= 1. <br/>
	/// Postcondition: On success, a video stream of the virtual machine's screen is sent, and the servers' response is returned. <br/>
	/// On failure, the video stream will not be sent and null is returned.
	/// </remarks>
	public async Task<MessageResponseVmStreamStart?> VirtualMachineStartStreamAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestVmStreamStart(true, id));
		if (result != ExitCode.Success)
		{
			return null;
		}

		return (MessageResponseVmStreamStart)response!;
	}
	
	/// <summary>
	/// Requests to stop the  video stream of the screen of the given virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine to stop the video stream of. id >= 1.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. id >= 1. <br/>
	/// Postcondition: On success, the video stream of the virtual machine's screen is stopped. <br/>
	/// On failure, the video stream continues, (if it was running) and the returned status will indicate the error.
	/// </remarks>
	public async Task<MessageResponseVmStreamStop.Status> VirtualMachineStopStreamAsync(int id)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestVmStreamStop(true, id));
		if (result != ExitCode.Success)
		{
			return MessageResponseVmStreamStop.Status.Failure;
		}

		return ((MessageResponseVmStreamStop)response!).Result;
	}

	/// <summary>
	/// Notify the server of a pointer movement on a virtual machines' screen.
	/// </summary>
	/// <param name="id">The ID of the virtual machine on which the pointer has moved. id >= 1.</param>
	/// <param name="position">The new pointer position on the virtual machines' screen. position != null.</param>
	/// <remarks>
	/// Precondition: The pointer has moved upon the screen of a virtual machine. id >= 1 &amp;&amp; position != null. <br/>
	/// Postcondition: The server is notified of the event.
	/// </remarks>
	public void NotifyPointerMovement(int id, Point position) =>
		SendInfo(new MessageInfoPointerMoved(true, id, position));

	/// <summary>
	/// Notifies the server of pointer buttons press/release event. <br/>
	/// Sends information about which mouse buttons are currently pressed.
	/// </summary>
	/// <param name="id">The ID of the virtual machine on which the pointer was clicked/released. id >= 1.</param>
	/// <param name="position">The current position of the pointer on the virtual machines screen. Must be in valid range.</param>
	/// <param name="pressedButtons">Flags for which buttons were pressed - See MouseButtons.</param>
	/// <remarks>
	/// Precondition: The pointer was clicked/released upon the screen of a virtual machine. position must be in valid range. id >= 1. <br/>
	/// Postcondition: The server is notified of the event.
	/// </remarks>
	public void NotifyPointerButtonEvent(int id, Point position, int pressedButtons) =>
		SendInfo(new MessageInfoPointerButtonEvent(true, id, position, pressedButtons));

	/// <summary>
	/// Notifies the server of a keyboard key event (pressed/released) on a virtual machine.
	/// </summary>
	/// <param name="id">The ID of the virtual machine on which the the key was pressed/released. id >= 1.</param>
	/// <param name="key">The key that was pressed/released.</param>
	/// <param name="pressed">Indicates whether the key was pressed or released (true=pressed, false=released).</param>
	/// <remarks>
	/// Precondition: A keyboard key was pressed or released upon the screen of a virtual machine. id >= 1. <br/>
	/// Postcondition: The server is notified of the event.
	/// </remarks>
	public void NotifyKeyboardKeyEvent(int id, PhysicalKey key, bool pressed) =>
		SendInfo(new MessageInfoKeyboardKeyEvent(true, id, key, pressed));
	
	/// <summary>
	/// Connects to the server. Retries connecting in time intervals if connection is denied or failed.
	/// </summary>
	/// <remarks>
	/// Precondition: Base service initialized. (base.Initialize() was called already) <br/>
	/// Postcondition: If the cancellation token did not require canceling the operation - the service is connected to the server. <br/>
	/// If the cancellation token required cancelling the operation, the service should be considered as not connected to the server.
	/// </remarks>
	private async Task ConnectToServerAsync()
	{
		while (!Cts.Token.IsCancellationRequested)
		{
			bool socketConnected = await SocketConnectToServer();
			if (socketConnected)
			{
				break;
			}

			if (System.OperatingSystem.IsBrowser())
			{
				await WebSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, null, Cts.Token);
				WebSocket.Dispose();
				WebSocket = new ClientWebSocket();
			}
			else
			{
				TcpSocket!.Close();
				TcpSocket.Dispose();
				TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			
				UdpSocket!.Close();
				UdpSocket.Dispose();
				UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			}
			
			try
			{
				await Task.Delay(SharedDefinitions.ConnectionDeniedRetryTimeout, Cts.Token);
			}
			catch (Exception)
			{
				return;
			}

			OnFailure(ExitCode.ConnectionToServerFailed);
		}
		
		Reconnected?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// Initialize TLS encryption and authenticate as the client.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: TcpSocket connected to the server. Not running on browser. <br/>
	/// Postcondition: On success, the TcpSslStream is encrypted and can be used for secure communication,
	/// the returned exit code indicates success. <br/>
	/// On failure, TcpSslStream is set to null, the TCP socket is disposed, and the returned exit code indicates the error.
	/// </remarks>
	private async Task<ExitCode> TlsInitialize()
	{
		if (System.OperatingSystem.IsBrowser() || !IsConnected())
			return ExitCode.CallOnInvalidCondition;

		NetworkStream networkStream = new NetworkStream(TcpSocket!, true);
		TcpSslStream = new SslStream(networkStream, false,
			(sender, certificate, chain, errors) => errors == SslPolicyErrors.None
		);

		try
		{
			await TcpSslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
			{
				TargetHost = SharedDefinitions.ServerIp,
				EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
				CertificateRevocationCheckMode = X509RevocationMode.Online
			});
		}
		catch (AuthenticationException)
		{
			await TcpSslStream.DisposeAsync();
			TcpSslStream = null;
			return ExitCode.AuthenticationFailed;
		}
		catch (Exception)
		{
			await TcpSslStream.DisposeAsync();
			TcpSslStream = null;
			return ExitCode.DisconnectedFromServer;
		}
		
		return ExitCode.Success;
	}

	/// <summary>
	/// Tries to perform a socket connection to the server.
	/// </summary>
	/// <returns>
	/// True if the connection has succeeded, false otherwise.
	/// </returns>
	/// <remarks>
	/// Precondition: Base service initialized. (base.Initialize() was called already) <br/>
	/// Postcondition: On success, (indicated by the returned value being true) the socket will be connected to the server. <br/>
	/// On failure, (indicated by the return value being false) the socket is not connected to the server.
	/// </remarks>
	private async Task<bool> SocketConnectToServer()
	{
		if (IsConnected() && IsInitialized())
			return false;

		/* Connect to the server. On connection failure try connecting with a 3-second delay between each try. */
		try
		{
			if (System.OperatingSystem.IsBrowser())
			{
				ClientWebSocket webSocket = (ClientWebSocket)WebSocket!;
				await webSocket.ConnectAsync(new Uri($"ws://{SharedDefinitions.ServerIp}:{SharedDefinitions.ServerTcpWebPort}/"), Cts.Token);
				IsServiceInitialized = webSocket.State == WebSocketState.Open;
				return IsServiceInitialized;
			}
			
			await TcpSocket!.ConnectAsync(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerTcpPort, Cts.Token);
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception)
		{
			OnFailure(ExitCode.ConnectionToServerFailed);
			return false;
		}
		
		/* TCP socket is connected. Now try connecting the UDP socket. */
		try
		{
			UdpSocket!.Bind(new IPEndPoint(IPAddress.Any, 0));
			await UdpSocket!.ConnectAsync(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerUdpPort);
		}
		catch (Exception)
		{
			TcpSocket!.Close();
			UdpSocket!.Close();
			return false;
		}

		if (UdpSocket.LocalEndPoint == null)
		{
			TcpSocket!.Close();
			UdpSocket!.Close();
			return false;		
		}

		int localPort = ((IPEndPoint)UdpSocket.LocalEndPoint).Port;
		if (localPort < IPEndPoint.MinPort || localPort > IPEndPoint.MaxPort)
		{
			TcpSocket!.Close();
			UdpSocket!.Close();
			return false;				
		}

		IsServiceInitialized = true;
		SendInfo(new MessageInfoIdentifyUdpPort(true, localPort));

		return true;
	}

	/// <summary>
	/// Handles requests from the server.
	/// </summary>
	/// <param name="request">
	/// The request that was sent by the server. request != null.
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. A request has been sent from the server. request != null. <br/>
	/// Postcondition: The request is considered as handled.
	/// </remarks>
	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result = ExitCode.Success;
		switch (request)
		{
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
	/// Handles message info's from the server.
	/// </summary>
	/// <param name="info">
	/// The info message that was sent by the server. info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp).
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. An info message has been sent from the server.
	/// info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp) <br/>
	/// Postcondition: The info message is considered as handled.
	/// </remarks>
	protected override async Task ProcessInfoAsync(Message info)
	{
		await base.ProcessInfoAsync(info);
		
		switch (info)
		{
			case MessageInfoCryptoUdp infoCryptoUdp:
			{
				ResetUdpCrypto(infoCryptoUdp.Key32, infoCryptoUdp.Salt4);
				break;
			}
			case MessageInfoSubUserCreated infoSubUserCreated:
			{
				SubUserCreated?.Invoke(this, infoSubUserCreated.SubUser);
				break;
			}
			case MessageInfoVmCreated infoVmCreated:
			{
				VmCreated?.Invoke(this, infoVmCreated.Descriptor);
				break;
			}
			case MessageInfoVmDeleted infoVmDeleted:
			{
				VmDeleted?.Invoke(this, infoVmDeleted.VmId);
				break;
			}
			case MessageInfoVmScreenFrame infoVmScreenFrame:
			{
				VmScreenFrameReceived?.Invoke(this, infoVmScreenFrame);
				break;
			}
			case MessageInfoVmAudioPacket infoVmAudioPacket:
			{
				VmAudioPacketReceived?.Invoke(this, infoVmAudioPacket);
				break;
			}
			case MessageInfoVmPoweredOn infoVirtualMachineData:
			{
				VmPoweredOn?.Invoke(this, infoVirtualMachineData.VmId);
				break;
			}
			case MessageInfoVmPoweredOff infoVmPoweredOff:
			{
				VmPoweredOff?.Invoke(this, infoVmPoweredOff.VmId);
				break;
			}
			case MessageInfoVmCrashed infoVmCrashed:
			{
				VmCrashed?.Invoke(this, infoVmCrashed.VmId);
				break;
			}
			case MessageInfoDriveCreated infoDriveCreated:
			{
				DriveCreated?.Invoke(this, infoDriveCreated.Descriptor);
				break;
			}
			case MessageInfoItemCreated infoItemCreated:
			{
				ItemCreated?.Invoke(this, infoItemCreated);
				break;
			}
			case MessageInfoItemDeleted infoItemDeleted:
			{
				ItemDeleted?.Invoke(this, infoItemDeleted);
				break;
			}
			case MessageInfoDriveConnected infoDriveConnected:
			{
				DriveConnected?.Invoke(this, 
					new DriveConnection(infoDriveConnected.DriveId, infoDriveConnected.VmId)
				);
				break;
			}
			case MessageInfoDriveDisconnected infoDriveDisconnected:
			{
				DriveDisconnected?.Invoke(this,
					new DriveConnection(infoDriveDisconnected.DriveId, infoDriveDisconnected.VmId)
				);
				break;
			}
		}
	}

	/// <summary>
	/// Handles a sudden disconnection from the server. Tries to reconnect. Reconnection can be canceled by using a cancellation token. (the parameter)
	/// </summary>
	/// <remarks>
	/// Precondition: A sudden disconnection from the server has occured. <br/>
	/// Postcondition: If the given cancellation token does not require cancellation, the service will be connected to the server. <br/>
	/// If the given cancellation token has required cancellation, the function returns and the service is considered as not connected to the server.
	/// </remarks>
	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
		ConnectToServerAsync().Wait();
	}
}