using System;
using System.Collections.Concurrent;
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

	public ClientService()
		: base(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
	{
	}

	public override async Task InitializeAsync()
	{
		if (IsInitialized())
			return;

		await ConnectToServerAsync();
		await base.InitializeAsync();
	}

	private async Task ConnectToServerAsync()
	{
		if (IsConnected() && IsInitialized())
			return;

		/* Connect to the server. On connection failure try connecting with a 3-second delay between each try. */
		while (true)
		{
			try
			{
				await _socket.ConnectAsync(IPAddress.Parse(Shared.SharedDefinitions.ServerIp),
					Shared.SharedDefinitions.ServerPort);
				break; /* Runs only if there was no exception (on exception it jumps to the catch block) */
			}
			catch (Exception)
			{
				OnFailure(ExitCode.ConnectionToServerFailed);
				await Task.Delay(3000);
			}
		}
	}
}