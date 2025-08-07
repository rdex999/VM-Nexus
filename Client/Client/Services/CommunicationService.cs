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
public class CommunicationService
{
	private Socket _socket;
	private Thread _thread;								/* Runs the Communicate function */
	private CancellationTokenSource _cts;
	private bool _isInitialized;
	private ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>> _responses;
	public event EventHandler<ExitCode> FailEvent;

	public CommunicationService()
	{
		_isInitialized = false;
		_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		_cts = new CancellationTokenSource();
		_thread = new Thread(() => Communicate(_cts.Token));
		_responses =  new ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>>();
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
				case MessageResponse response:
				{
					TaskCompletionSource<MessageResponse> tcs = new TaskCompletionSource<MessageResponse>();
					tcs.SetResult(response);
					_responses[response.Id] = tcs;
					break;
				}
				case MessageRequest request:
				{
					throw new NotImplementedException();
					break;
				}
				default:
				{
					throw new NotImplementedException();
					break;
				}
			}
		}
	}

	private async Task<MessageResponse?> SendRequestAsync(MessageRequest message)
	{
		/* The loop might take a while, so put it inside Task.Run and await it */
		/* Use a cancellation for canceling the operation after 3 seconds if it didnt work */
		CancellationTokenSource cts = new CancellationTokenSource();
		cts.CancelAfter(SharedDefinitions.MessageTimeoutMilliseconds);
		try
		{
			await Task.Run(() =>
			{
				ExitCode result;
				do
				{
					result = SendMessage(message);
					cts.Token.ThrowIfCancellationRequested();
				} while (result != ExitCode.Success);
			}, cts.Token);
		}
		catch (OperationCanceledException)	/* If three seconds have passed and the message wasnt sent, abort. */
		{
			return null;
		}
		cts.Dispose();
		
		/* Here, the message was sent successfully */
		/* Now wait for the response for this specific request that we just send */
		cts = new CancellationTokenSource();
		cts.CancelAfter(SharedDefinitions.MessageTimeoutMilliseconds);
		Task<MessageResponse>? response = null;
		try
		{
			await Task.Run(() =>
			{
				while (true)
				{
					if (_responses.TryRemove(message.Id, out TaskCompletionSource<MessageResponse>? tcs))
					{
						response = tcs.Task;
						break;
					}
					cts.Token.ThrowIfCancellationRequested();
				}
			}, cts.Token);
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		cts.Dispose();
	
		if(response != null)
			return await response;
		
		return null;
	}

	/* On disconnection, stop trying to send the message. The message must be sent again from its beginning. */
	private ExitCode SendMessage(Message message)
	{
		ExitCode result;
		
		byte[] bytes = Common.ToByteArray(message);
		byte[] sizeInBytes = BitConverter.GetBytes(bytes.Length);
		result = SendBytesExact(sizeInBytes);
		
		if (result != ExitCode.Success)
			return result;
		
		result = SendBytesExact(sizeInBytes);
		
		return result;
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

	private ExitCode SendBytesExact(byte[] bytes)
	{
		int bytesSent = 0;
		while (bytesSent < bytes.Length)
		{
			int sent = _socket.Send(bytes, bytesSent, bytes.Length - bytesSent, SocketFlags.None);
			if (sent <= 0)
				return ExitCode.DisconnectedFromServer;
			
			bytesSent += sent;
		}
		return ExitCode.Success;
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