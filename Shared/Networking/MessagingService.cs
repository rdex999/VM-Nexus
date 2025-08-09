using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

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
		return IsInitialized() && _socket.Connected;
	}

	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			if (!IsConnected() && !token.IsCancellationRequested)
			{
				HandleSuddenDisconnection();
			}
			
			Message? message = ReceiveMessage();
			if (message == null)
			{
				if (!IsConnected() && !token.IsCancellationRequested)
				{
					HandleSuddenDisconnection();
				}
				continue;	
			}

			switch (message)
			{
				case MessageResponse response:
				{
					if (_responses.TryRemove(response.RequestId, out var tcs))
					{
						tcs.SetResult(response);
					}
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
		
		Disconnect();
		AfterDisconnection();
	}

	protected async Task<(MessageResponse?, ExitCode)> SendRequestAsync(MessageRequest message)
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
			return (null, ExitCode.MessageSendingTimeout);
		}
		cts.Dispose();
		
		/* Here, the message was sent successfully */
		TaskCompletionSource<MessageResponse>? tcs = new TaskCompletionSource<MessageResponse>();
		_responses[message.Id] = tcs;
		
		/* Now wait for the response for this specific request that we just send */
		ExitCode result = ExitCode.Success;
		MessageResponse response = null;
		try
		{
			response = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(SharedDefinitions.MessageTimeoutMilliseconds));
		}
		catch (TimeoutException)
		{
			result = ExitCode.MessageSendingTimeout;
		}

		if (result != ExitCode.Success)
		{
			_responses.TryRemove(message.Id, out _);
		}
	
		return (response, result);
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
				while (result != ExitCode.Success)
				{
					result = SendMessage(response);
					
					if (result == ExitCode.DisconnectedFromServer)
					{
						/* Then need to send the request itself again, so return and exit from this function */
						break;
					}
					cts.Token.ThrowIfCancellationRequested();
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
		
		byte[] bytes = Common.ToByteArrayWithType(message);
		byte[] sizeInBytes = BitConverter.GetBytes(bytes.Length);
		result = SendBytesExact(sizeInBytes);
		
		if (result != ExitCode.Success)
			return result;
		
		result = SendBytesExact(bytes);
		
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

		return (Message)Common.FromByteArrayWithType(messageBytes)!;
	}

	private ExitCode SendBytesExact(byte[] bytes)
	{
		int bytesSent = 0;
		while (bytesSent < bytes.Length)
		{
			int sent;
			try
			{
				sent = _socket.Send(bytes, bytesSent, bytes.Length - bytesSent, SocketFlags.None);
			}
			catch (Exception e)
			{
				return ExitCode.DisconnectedFromServer;
			}

			if (sent <= 0)
			{
				return ExitCode.DisconnectedFromServer;
			}
			
			bytesSent += sent;
		}
		return ExitCode.Success;
	}

	private byte[]? ReceiveBytesExact(int size)
	{
		if (size <= 0)
			return null;
		
		byte[] bytes = new byte[size];
		int bytesRead = 0;
		while (bytesRead < size)
		{
			int currentRead;
			try
			{
				currentRead = _socket.Receive(bytes, bytesRead, size - bytesRead, SocketFlags.None);
			}
			catch (Exception e)
			{
				return null;
			}
			
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

	public void Disconnect()
	{
		if (_cts != null)
		{
			_cts.Cancel();
		}
		_socket.Dispose();
		_socket.Close();
		if (Thread.CurrentThread != _thread)
		{
			_thread.Join();
		}
		
		if (_cts != null)
		{
			_cts.Dispose();
			_cts = null;
		}
	}

	protected virtual async Task ProcessRequestAsync(MessageRequest request)
	{
	}

	protected virtual void HandleSuddenDisconnection()
	{
		OnFailure(ExitCode.DisconnectedFromServer);
		AfterDisconnection();
	}
	
	protected virtual void AfterDisconnection()
	{
	}
}
