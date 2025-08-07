using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Shared.Networking;

public class MessagingService
{
	protected Socket _socket;
	private Thread _thread;								/* Runs the Communicate function */
	private CancellationTokenSource _cts;
	protected bool _isInitialized;
	private ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>> _responses;
	
	public event EventHandler<ExitCode> FailEvent;

	public MessagingService(Socket socket)
	{
		_isInitialized = false;
		_socket = socket;
		_cts = new CancellationTokenSource();
		_thread = new Thread(() => Communicate(_cts.Token));
		_responses =  new ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>>();
	}

	public virtual async Task InitializeAsync()
	{
		_isInitialized = true;
		_thread.Start();
	}

	public bool IsInitialized()
	{
		return _isInitialized;
	}

	public bool IsConnected()
	{
		return _socket.Connected;
	}

	public virtual void Disconnect()
	{
		/* TODO: Add logic to send a disconnection request */
		_cts.Cancel();
	}

	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			if (!IsConnected())
			{
				HandleSuddenDisconnection();
			}
			
			Message? message = ReceiveMessage();
			if (message == null)
			{
				if (!IsConnected())
				{
					HandleSuddenDisconnection();
				}
				continue;	
			}

			switch (message)
			{
				case MessageResponse response:
				{
					TaskCompletionSource<MessageResponse> tcs = new TaskCompletionSource<MessageResponse>();
					tcs.SetResult(response);
					_responses[response.RequestId] = tcs;
					break;
				}
				case MessageRequest request:
				{
					_ = HandleRequestAsync(token, request);
					break;
				}
				default:
				{
					throw new NotImplementedException();
					break;
				}
			}
		}
		
		_cts.Dispose();
		_socket.Dispose();
		_socket.Close();
		
		AfterDisconnection();
	}

	protected async Task<MessageResponse?> SendRequestAsync(MessageRequest message)
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
				cts.Token.ThrowIfCancellationRequested();
				while (true)
				{
					if (_responses.TryRemove(message.Id, out TaskCompletionSource<MessageResponse>? tcs))
					{
						response = tcs.Task;
						break;
					}
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

	protected async Task<ExitCode> SendResponse(MessageResponse response)
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		cts.CancelAfter(SharedDefinitions.MessageTimeoutMilliseconds);

		ExitCode result = SendMessage(response);
		try
		{
			await Task.Run(() =>
			{
				while (!cts.Token.IsCancellationRequested && result != ExitCode.Success)
				{
					result = SendMessage(response);
					
					if (result == ExitCode.DisconnectedFromServer)
					{
						/* Then need to send the request itself again, so return and exit from this function */
						break;
					}
				}
			},  cts.Token);
		}
		catch (OperationCanceledException)
		{
			result = ExitCode.MessageSendingTimeout;
		}
		
		cts.Dispose();

		return result;
	}

	private async Task HandleRequestAsync(CancellationToken token, MessageRequest request)
	{
		try
		{
			token.ThrowIfCancellationRequested();
			await ProcessRequestAsync(request);
		}
		catch (OperationCanceledException)
		{
		}
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

	protected void OnFailure(ExitCode code)
	{
		FailEvent?.Invoke(this, code);
	}

	protected virtual void AfterDisconnection()
	{
	}
	
	protected virtual async Task ProcessRequestAsync(MessageRequest request)
	{
	}

	protected virtual void HandleSuddenDisconnection()
	{
		OnFailure(ExitCode.DisconnectedFromServer);
	}
}
