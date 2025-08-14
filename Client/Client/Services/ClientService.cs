using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public event EventHandler? Reconnected;
	
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

		base.Initialize(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
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
	public async Task<bool> IsUsernameAvailableAsync(string username)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCheckUsername(true, username));
		
		return result == ExitCode.Success && ((MessageResponseCheckUsername)response!).Available;
	}

	/// <summary>
	/// Sends a request to crete a new account with the given username and password.
	/// </summary>
	/// <param name="username">
	/// The username of the new user. username != null.
	/// </param>
	/// <param name="password">
	/// The password for the new user. password != null.
	/// </param>
	/// <returns>
	/// A status indicating the result of the account creation request.
	/// </returns>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the server. username != null &amp;&amp; password != null. <br/>
	/// Postcondition: On success, a new account with the given username and password is created. The returned value will indicate success. <br/>
	/// On failure, the account is not created, and the returned value will indicate and error.
	/// </remarks>
	public async Task<MessageResponseCreateAccount.Status> CreateAccountAsync(string username, string password)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateAccount(true, username, password));
		if (result != ExitCode.Success)
			return MessageResponseCreateAccount.Status.Failure;
		
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

			MessagingSocket!.Close();
			MessagingSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

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
		if (!CommunicationThread!.IsAlive)
		{
			CommunicationThread.Start();
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
			MessagingSocket!.Connect(IPAddress.Parse(Shared.SharedDefinitions.ServerIp), Shared.SharedDefinitions.ServerPort);
			return true;
		}
		catch (Exception)
		{
			OnFailure(ExitCode.ConnectionToServerFailed);
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
			default:
			{
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