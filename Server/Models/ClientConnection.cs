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
	private bool _hasDisconnected = false;		/* Has the Disconnect function ran? */

	public ClientConnection(Socket socket, DatabaseService databaseService)
		: base()
	{
		_databaseService = databaseService;
		InitializeAsync(socket).Wait();	/* Doesnt contain long-running code, so its fine to just Wait() it here */
		_isInitialized = true;
		_thread.Start();
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

			case MessageRequestLogin reqLogin:
			{
				bool validLogin = await _databaseService.IsValidLoginAsync(reqLogin.Username, reqLogin.Password);
				result = await SendResponse(new MessageResponseLogin(true, reqLogin.Id, validLogin));
				if (result == ExitCode.Success)
				{
					_isLoggedIn = validLogin;
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

	public override void Disconnect()
	{
		base.Disconnect();
		AfterDisconnection();
	}
	
	/* When the client suddenly disconnects, delete this client object, and let it be re-created in ListenForClients */
	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
	}

	protected override void AfterDisconnection()
	{
		if (!_hasDisconnected) /* To prevent recursion */
		{
			_hasDisconnected = true;
			base.AfterDisconnection();
			base.Disconnect();
			Disconnected?.Invoke(this, EventArgs.Empty);
		}
	}
}
