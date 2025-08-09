using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.Services;
using Shared;
using Shared.Networking;

namespace Server.Models;

public sealed class ClientConnection : MessagingService
{
	public event EventHandler Disconnected;
	private bool _isLoggedIn = false;
	private DatabaseService _databaseService;

	public ClientConnection(Socket socket, DatabaseService databaseService)
		: base(socket)
	{
		_databaseService = databaseService;
		InitializeAsync().Wait();	/* Doesnt contain long-running code, so its fine to just Wait() it here */
	}

	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result;
		switch (request)
		{
			case MessageRequestConnect reqConnect:
			{
				MessageResponse response = new MessageResponseConnect(true, reqConnect.Id, true);
				result = await SendResponse(response);
				break;
			}

			case MessageRequestDisconnect reqDisconnect:
			{
				result = await SendResponse(new MessageResponseDisconnect(true, reqDisconnect.Id));
				Disconnect();
				AfterDisconnection();
				break;
			}

			case MessageRequestCheckUsername reqCheckUsername:
			{
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(reqCheckUsername.Username);
				result = await SendResponse(new MessageResponseCheckUsername(true, reqCheckUsername.Id, usernameAvailable));
				break;
			}

			case MessageRequestCreateAccount reqCreateAccount:
			{
				/* TODO: Register into database */
				bool usernameAvailable = !await _databaseService.IsUserExistAsync(reqCreateAccount.Username);
				MessageResponseCreateAccount.Status status;
				if (usernameAvailable)
				{
					ExitCode code = await _databaseService.RegisterUserAsync(reqCreateAccount.Username, reqCreateAccount.Password);
					if (code == ExitCode.Success)
					{
						status = MessageResponseCreateAccount.Status.Success;
					}
					else
					{
						status = MessageResponseCreateAccount.Status.Failure;
					}
				}
				else
				{
					status = MessageResponseCreateAccount.Status.UsernameNotAvailable;
				}
				
				result = await SendResponse(new MessageResponseCreateAccount(true, reqCreateAccount.Id, status));
				if (result == ExitCode.Success)
				{
					_isLoggedIn = true;
				}
				break;
			}
				
			default:
			{
				throw new NotImplementedException();
			}
		}

		switch (result)
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
	
	/* When the client suddenly disconnects, delete this client object, and let it be re-created in ListenForClients */
	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
	}

	protected override void AfterDisconnection()
	{
		base.AfterDisconnection();
		Disconnected?.Invoke(this, EventArgs.Empty);
	}

	public async Task DisconnectClient()
	{
		/* It doesnt matter what the client says (the client shall always accept a disconnection) - What can he do? haha */
		await SendRequestAsync(new MessageRequestDisconnect(true));
		Disconnect();
	}
}
