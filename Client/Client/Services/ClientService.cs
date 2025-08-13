using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public event EventHandler? Reconnected;
	
	public async Task InitializeAsync()
	{
		if (IsInitialized())
			return;

		base.Initialize(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
		await ConnectToServerAsync();
	}

	public void OnExit()
	{
		Disconnect();
	}

	public async Task<bool> IsUsernameAvailableAsync(string username)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCheckUsername(true, username));
		
		return result == ExitCode.Success && ((MessageResponseCheckUsername)response!).Available;
	}

	public async Task<MessageResponseCreateAccount.Status> CreateAccountAsync(string username, string password)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestCreateAccount(true, username, password));
		if (result != ExitCode.Success)
			return MessageResponseCreateAccount.Status.Failure;
		
		MessageResponseCreateAccount reqCreateAccount = (MessageResponseCreateAccount)response!;
		return reqCreateAccount.Result;
	}

	public async Task<bool?> LoginAsync(string username, string password)
	{
		(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestLogin(true, username, password));
		if (result != ExitCode.Success)
			return null;
		
		MessageResponseLogin reqLogin = (MessageResponseLogin)response!;
		return reqLogin.Accepted;
	}

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

	private async Task ConnectToServerAsync(CancellationToken? token = null)
	{
		while (token == null || !token.Value.IsCancellationRequested)
		{
			bool socketConnected = SocketConnectToServer();
			if (socketConnected)
			{
				break;
			}

			MessagingSocket.Close();
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
		if (!CommunicationThread.IsAlive)
		{
			CommunicationThread.Start();
		}

		Reconnected?.Invoke(this, EventArgs.Empty);
	}
	
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

	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result;
		switch (request)
		{
			default:
			{
				throw new NotImplementedException();
			}
		}

		switch (result)
		{
			case ExitCode.DisconnectedFromServer:
			{
				HandleSuddenDisconnection();
				break;
			}

			default:
			{
				throw new NotImplementedException();
			}
		}
		
	}

	protected override void HandleSuddenDisconnection(CancellationToken? token = null)
	{
		base.HandleSuddenDisconnection(token);
		ConnectToServerAsync(token).Wait();
	}
}