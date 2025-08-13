using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Server.Services;
using Shared;
using Shared.Networking;

namespace Server.Models;

public sealed class ClientConnection : MessagingService
{
	public event EventHandler Disconnected;
	private bool _isLoggedIn = false;
	private string _username = string.Empty;
	private DatabaseService _databaseService;
	private bool _hasDisconnected = false;		/* Has the Disconnect function ran? */

	public ClientConnection(Socket socket, DatabaseService databaseService)
		: base()
	{
		_databaseService = databaseService;
		Initialize(socket);
		IsServiceInitialized = true;
		CommunicationThread.Start();
	}

	protected override async Task ProcessRequestAsync(MessageRequest request)
	{
		await base.ProcessRequestAsync(request);

		ExitCode result;
		switch (request)
		{
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
					_username = reqCreateAccount.Username;
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
					_username = reqLogin.Username;
				}
				break;
			}

			case MessageRequestLogout reqLogout:
			{
				if (_isLoggedIn)
				{
					_isLoggedIn = false;
					_username = string.Empty;
					await Task.Delay(50);		/* If i dont do this, the client gets a response timeout - like the client doesnt receive the response.. */
					result = await SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.Success));
				}
				else
				{
					result = await SendResponse(new MessageResponseLogout(true,  reqLogout.Id, MessageResponseLogout.Status.UserNotLoggedIn));
				}

				break;
			}
				
			default:
			{
				result = ExitCode.Success;
				break;
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
