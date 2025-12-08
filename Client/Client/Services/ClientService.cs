using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Input;
using Shared;
using Shared.Drives;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = Shared.VirtualMachines.OperatingSystem;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public event EventHandler? Reconnected;
	public event EventHandler<VmGeneralDescriptor>? VmCreated;
	public event EventHandler<int>? VmDeleted;
	public event EventHandler<MessageInfoVmScreenFrame>? VmScreenFrameReceived;
	public event EventHandler<MessageInfoVmAudioPacket>? VmAudioPacketReceived;
	public event EventHandler<int>? VmPoweredOn;
	public event EventHandler<int>? VmPoweredOff;
	public event EventHandler<int>? VmCrashed;
	public event EventHandler<DriveGeneralDescriptor>? DriveCreated;
	public event EventHandler<MessageInfoItemDeleted>? ItemDeleted;
	public event EventHandler<DriveConnection>? DriveConnected;
	public event EventHandler<DriveConnection>? DriveDisconnected;
	
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

		TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
		
		await ConnectToServerAsync();
		
		StartTcp();
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
			return MessageResponseCreateAccount.Status.CredentialsCannotBeEmpty;
		
		MessageResponseCreateAccount reqCreateAccount = (MessageResponseCreateAccount)response!;
		return reqCreateAccount.Result;
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
		
		MessageResponseLogin reqLogin = (MessageResponseLogin)response!;
		return reqLogin.Accepted;
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
		{
			return  MessageResponseLogout.Status.Failure;
		}
		
		MessageResponseLogout resLogout = (MessageResponseLogout)response!;
		return resLogout.Result;
	}

	/// <summary>
	/// Sends a create VM request.
	/// </summary>
	/// <param name="name">The name of the new virtual machine. name != null.</param>
	/// <param name="operatingSystem">The operating system of the new virtual machine. operatingSystem != null.</param>
	/// <param name="cpuArchitecture">The CPU architecture to be used for the virtual machine. cpuArchitecture != null.</param>
	/// <param name="bootMode">The boot mode - UEFI or BIOS. bootMode != null.</param>
	/// <returns>The result of the VM creation operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User must be logged in. name != null <br/>
	/// Postcondition: On success, a new virtual machine is created and the servers response is returned. <br/>
	/// On networking failure, null is returned. On other failure, the servers response is returned.
	/// </remarks>
	public async Task<MessageResponseCreateVm?> CreateVirtualMachineAsync(string name,
		OperatingSystem operatingSystem, CpuArchitecture cpuArchitecture, BootMode bootMode)
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(
			new MessageRequestCreateVm(true, name, operatingSystem, cpuArchitecture, bootMode)
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
	/// Requests to create a CD-ROM drive. Starts uploading the ISO image.
	/// </summary>
	/// <param name="name">The name for the new drive. Must be unique for the user. name != null.</param>
	/// <param name="iso">A stream of the ISO image. Used while uploading the ISO. Disposed when done. iso != null.</param>
	/// <returns>An upload handler managing the upload of the ISO image, or null on failure.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. The given name must be unique for the user.
	/// The given ISO image stream should be formatted with the ISO 9660 file system. name != null &amp;&amp; iso != null. <br/>
	/// Postcondition: On success, the upload is started and an upload handler handling the upload is returned. On failure, null is returned.
	/// </remarks>
	public async Task<UploadHandler?> CreateDriveCdromAsync(string name, Stream iso)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateDriveCdrom(true, name, (ulong)iso.Length));
		if (result != ExitCode.Success)
			return null;

		MessageResponseCreateDriveCdrom res = (MessageResponseCreateDriveCdrom)response!;
		if (res.Result != MessageResponseCreateDriveCdrom.Status.Success)
			return null;
		
		UploadHandler handler = new UploadHandler(this, iso);
		handler.Start(res.CdromTransferId);

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
	public async Task<DownloadHandlerFileSave?> StartItemDownloadAsync(int driveId, string path, string destination)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDownloadItem(true, driveId, path));
		if (result != ExitCode.Success)
			return null;

		MessageResponseDownloadItem res = (MessageResponseDownloadItem)response!;
		if (res.Result != MessageResponseDownloadItem.Status.Success)
			return null;
		
		DownloadHandlerFileSave handler;
		try
		{
			handler = new DownloadHandlerFileSave(res.ItemSize, destination);
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
	public async Task<DownloadHandlerFileSave?> StartItemDownloadAsync(int driveId, string path, Stream destination)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestDownloadItem(true, driveId, path));
		if (result != ExitCode.Success)
			return null;

		MessageResponseDownloadItem res = (MessageResponseDownloadItem)response!;
		if (res.Result != MessageResponseDownloadItem.Status.Success)
			return null;
		
		DownloadHandlerFileSave handler = new DownloadHandlerFileSave(res.ItemSize, destination);
		handler.Start(res.StreamId);
		AddTransfer(handler);

		return handler;
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
	/// <param name="token">
	/// Optional cancellation token to cancel the operation if required. <br/>
	/// Because this function runs until a connection is established, it may be required to cancel the operation (For example, if the user exists the application)
	/// </param>
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

			TcpSocket!.Close();
			TcpSocket.Dispose();
			TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			
			UdpSocket!.Close();
			UdpSocket.Dispose();
			UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

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
			await TcpSocket!.ConnectAsync(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerTcpPort,
				Cts.Token);
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
		SendInfo(new MessageInfoIdentifyUdp(true, localPort));

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