using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

namespace Server.Models;

public sealed class Client : MessagingService
{
	public event EventHandler Disconnected;

	public Client(Socket socket)
		: base(socket)
	{
		InitializeAsync().Wait();	/* Doesnt contain long-running code, so its fine to just Wait() it here */
	}

	/* When the client suddenly disconnects, delete this client object, and let it be re-created in ListenForClients */
	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
		AfterDisconnection();
	}

	protected override void AfterDisconnection()
	{
		base.AfterDisconnection();
		Disconnected?.Invoke(this, EventArgs.Empty);
	}

	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode code;
		switch (request)
		{
			case MessageRequestConnect req:
			{
				MessageResponse response = new MessageResponseConnect(true, req.Id, true);
				code = await SendResponse(response);
				break;
			}
			default:
			{
				throw new NotImplementedException();
				break;
			}
		}

		switch (code)
		{
			case ExitCode.Success:
				return;
			
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
}