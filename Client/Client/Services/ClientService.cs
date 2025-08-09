using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Client.Services;

/* Responsible for communicating with the server */
public class ClientService : MessagingService
{
	public ClientService()
		: base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
	{
	}

	public override async Task InitializeAsync()
	{
		if (IsInitialized())
			return;

		await SocketConnectToServerAsync();
		await base.InitializeAsync();
		await RequestConnectAsync();
	}

	public async Task OnExit()
	{
		await DisconnectFromServerAsync();
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

	private async Task ConnectToServerAsync()
	{
		await SocketConnectToServerAsync();
		await RequestConnectAsync();
	}
	
	private async Task<ExitCode> DisconnectFromServerAsync()
	{
		if(!IsConnected() || !IsInitialized())
			return ExitCode.Success;
			
		(MessageResponse? _, ExitCode result) = await SendRequestAsync(new  MessageRequestDisconnect(true));
		Disconnect();
		return result;
	}
	
	private async Task RequestConnectAsync()
	{
		bool connected = false;
		while (!connected)
		{
			(MessageResponse? response, ExitCode result) = await SendRequestAsync(new MessageRequestConnect(true));
			if (result != ExitCode.Success)
			{
				/* TODO: Handle errors here */
				throw new NotImplementedException();
			}

			MessageResponseConnect resConnect = (MessageResponseConnect)response;
			connected = resConnect.Accepted;
			if (!connected)		/* If the connection was denied by the server, then it probably has a lot of clients, we should wait a bit and then retry */
			{
				await Task.Delay(SharedDefinitions.ConnectionDeniedRetryTimeout);
				/* TODO: Add logic to display error message on UI */
			}
		}
	}
	
	private async Task SocketConnectToServerAsync()
	{
		if (IsConnected() && IsInitialized())
			return;

		/* Connect to the server. On connection failure try connecting with a 3-second delay between each try. */
		while (true)
		{
			try
			{
				await _socket.ConnectAsync(IPAddress.Parse(Shared.SharedDefinitions.ServerIp), Shared.SharedDefinitions.ServerPort);
				break; /* Runs only if there was no exception (on exception it jumps to the catch block) */
			}
			catch (Exception)
			{
				OnFailure(ExitCode.ConnectionToServerFailed);
				await Task.Delay(3000);
			}
		}
	}

	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result;
		switch (request)
		{
			case MessageRequestDisconnect reqDisconnect:
			{
				result = await SendResponse(new MessageResponseDisconnect(true, reqDisconnect.Id));
				Disconnect();
				HandleSuddenDisconnection();
				break;
			}

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

	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
		ConnectToServerAsync().Wait();
	}
}