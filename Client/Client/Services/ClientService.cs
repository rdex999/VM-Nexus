using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Shared;
using Shared.Networking;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public event EventHandler? Reconnected;
	public event EventHandler<SharedDefinitions.VmGeneralDescriptor[]>? VmListChanged;
	public event EventHandler<MessageInfoVmScreenFrame>? VmScreenFrameReceived;
	public event EventHandler<MessageInfoVmAudioPacket>? VmAudioPacketReceived;
	public event EventHandler<int>? VmPoweredOn;
	public event EventHandler<int>? VmPoweredOff;
	public event EventHandler<int>? VmCrashed;
	
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

		Initialize(
			new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
			new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
		);
		await ConnectToServerAsync();
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
		SharedDefinitions.OperatingSystem operatingSystem, SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(
			new MessageRequestCreateVm(true, name, operatingSystem, cpuArchitecture, bootMode));

		if (response == null)
		{
			return null;
		}

		return (MessageResponseCreateVm)response;
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
	/// Requests the server to create a drive with the given parameters.
	/// </summary>
	/// <param name="name">The name of the drive. Must be unique for the user. name != null.</param>
	/// <param name="type">The type of drive. (NVMe, SSD, etc)</param>
	/// <param name="size">The size of the drive in MiB. size >= 1.</param>
	/// <param name="operatingSystem">The operating system to install on the drive.</param>
	/// <returns>A status indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. User is logged in. name != null &amp;&amp; size >= 1.<br/>
	/// Postcondition: On success, the drive is created and the servers response is returned. <br/>
	/// On failure, if there was a networking failure, null is returned. On other failures, the servers response is returned.
	/// </remarks>
	public async Task<MessageResponseCreateDrive?> CreateDriveAsync(
		string name, SharedDefinitions.DriveType type, int size, SharedDefinitions.OperatingSystem operatingSystem)
	{
		(MessageResponse? response, ExitCode _) = await SendRequestAsync(new MessageRequestCreateDrive(true, name, type, size, operatingSystem));
		if (response == null)
		{
			return null;
		}
		return (MessageResponseCreateDrive)response;
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
		{
			return MessageResponseConnectDrive.Status.Failure;
		}
		return ((MessageResponseConnectDrive)response!).Result;
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
	/// <param name="pressedButtons">Flags for which buttons were pressed - See SharedDefinitions.MouseButtons.</param>
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
	private async Task ConnectToServerAsync(CancellationToken? token = null)
	{
		while (token == null || !token.Value.IsCancellationRequested)
		{
			bool socketConnected = SocketConnectToServer();
			if (socketConnected)
			{
				break;
			}

			TcpSocket!.Close();
			TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			if (token == null)
			{
				await Task.Delay(SharedDefinitions.ConnectionDeniedRetryTimeout);
			}
			else
			{
				try
				{
					await Task.Delay(SharedDefinitions.ConnectionDeniedRetryTimeout, token.Value);
				}
				catch (Exception e)
				{
					return;
				}
			}
			OnFailure(ExitCode.ConnectionToServerFailed);
		}

		if (token != null && token.Value.IsCancellationRequested)
		{
			return;
		}

		IsServiceInitialized = true;
		if (!TcpCommunicationThread!.IsAlive)
		{
			TcpCommunicationThread.Start();
		}
		if (!UdpCommunicationThread!.IsAlive)
		{
			UdpCommunicationThread.Start();
		}
		if (!MessageSenderThread!.IsAlive)
		{
			MessageSenderThread.Start();
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
	private bool SocketConnectToServer()
	{
		if (IsConnected() && IsInitialized())
			return false;

		/* Connect to the server. On connection failure try connecting with a 3-second delay between each try. */
		try
		{
			TcpSocket!.Connect(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerTcpPort);
		}
		catch (Exception)
		{
			OnFailure(ExitCode.ConnectionToServerFailed);
			return false;
		}
		
		/* TCP socket is connected. Now try connecting the UDP socket. */
		try
		{
			UdpSocket!.Connect(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerUdpPort);
			return true;
		}
		catch (Exception e)
		{
			TcpSocket!.Close();
			return false;
		}
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
			case MessageInfoVmList infoVmList:
			{
				/* The server might send this info before HomeViewModel subscribes to the event, so wait for a subscriber. */
				while (VmListChanged == null || VmListChanged.GetInvocationList().Length < 1)
				{
					await Task.Delay(50);
				}
				VmListChanged?.Invoke(this, infoVmList.VmDescriptors);
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
		}
	}

	/// <summary>
	/// Handles a sudden disconnection from the server. Tries to reconnect. Reconnection can be canceled by using a cancellation token. (the parameter)
	/// </summary>
	/// <param name="token">
	/// Optional cancellation token, used to cancel the reconnection.
	/// </param>
	/// <remarks>
	/// Precondition: A sudden disconnection from the server has occured. <br/>
	/// Postcondition: If the given cancellation token does not require cancellation, the service will be connected to the server. <br/>
	/// If the given cancellation token has required cancellation, the function returns and the service is considered as not connected to the server.
	/// </remarks>
	protected override void HandleSuddenDisconnection(CancellationToken? token = null)
	{
		base.HandleSuddenDisconnection(token);
		ConnectToServerAsync(token).Wait();
	}
}