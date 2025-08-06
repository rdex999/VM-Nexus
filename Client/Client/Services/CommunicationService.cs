using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared;
using Shared.Networking;

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
			
			Message? message = ReceiveMessage();
			if (message == null)
			{
				if (!IsConnectedToServer())
				{
					HandleDisconnection().Wait(token);
				}
				continue;	
			}

			switch (message)
			{
				case MessageResponseConnect resConnect:
				{
					throw new NotImplementedException();
					break;
				}
				default:
					throw new NotImplementedException();
			}
		}
	}

	private Message? ReceiveMessage()
	{
		byte[]? messageSizeInBytes = ReceiveBytesExact(4);
		if (messageSizeInBytes == null || messageSizeInBytes.Length == 0)
		{
			return null;
		}
		int size =  BitConverter.ToInt32(messageSizeInBytes, 0);	/* If size is 0, then its an invalid message */
	
		byte[]? messageBytes = ReceiveBytesExact(size);
		if (messageBytes == null || messageBytes.Length == 0)
		{
			return null;
		}

		return Common.FromByteArray<Message>(messageBytes);
	}
	
	private Byte[]? ReceiveBytesExact(int size)
	{
		if (size <= 0)
			return null;
		
		Byte[] bytes = new Byte[size];
		int bytesRead = 0;
		while (bytesRead < size)
		{
			int currentRead = _socket.Receive(bytes, bytesRead, size - bytesRead, SocketFlags.None);
			if (currentRead <= 0)	/* Means that the socket was disconnected */
			{
				return null;
			}
			bytesRead += currentRead;
		}
		return bytes;
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