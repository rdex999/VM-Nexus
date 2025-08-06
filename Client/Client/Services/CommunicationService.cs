using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;

namespace Client.Services;

/* Responsible for communicating with the server */
public class CommunicationService
{
	private Socket _socket;
	private Thread _thread;								/* Runs the Communicate function */
	private CancellationTokenSource _cts;
	private bool _isInitialized;
	public event EventHandler<ExitCode> FailEvent;

	public CommunicationService()
	{
		_isInitialized = false;
		_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		_cts = new CancellationTokenSource();
		_thread = new Thread(() => Communicate(_cts.Token));
	}

	public async Task Initialize()
	{
		if (_isInitialized)
			return;
		
		await ConnectToServerAsync();
		_isInitialized = true;
		_thread.Start();
	}

	public bool IsInitialized()
	{
		return  _isInitialized;
	}

	private async Task ConnectToServerAsync()
	{
		if (IsConnectedToServer() && _isInitialized)
			return;
		
		/* Connect to the server. On connection failure try connecting with a 3-second delay between each try. */
		while (true)
		{
			try
			{
				await _socket.ConnectAsync(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerPort);
				break; /* Runs only if there was no exception (on exception it jumps to the catch block) */
			}
			catch (Exception)
			{
				OnFailure(ExitCode.ConnectionToServerFailed);
				await Task.Delay(3000);
			}
		}
	}

	public bool IsConnectedToServer()
	{
		return _socket.Connected;
	}
	
	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			if (!IsConnectedToServer())
			{
				HandleDisconnection().Wait(token);
			}
		}
	}

	private async Task HandleDisconnection()
	{
		OnFailure(ExitCode.DisconnectedFromServer);
		await ConnectToServerAsync();
		/* TODO: Add logic to display a message */
	}
	
	private void OnFailure(ExitCode code)
	{
		FailEvent?.Invoke(this, code);
	}


}