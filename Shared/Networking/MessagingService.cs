using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Shared.Networking;

public class MessagingService
{
	protected Socket? MessagingSocket;
	protected Thread? CommunicationThread;								/* Runs the Communicate function */
	private CancellationTokenSource? _cts;
	protected bool IsServiceInitialized;
	private ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>> _responses;
	protected Thread? MessageSenderThread;								/* Sends messages from the message queue, through the socket. */
	private ConcurrentQueue<Message>? _messageQueue;
	private ManualResetEventSlim? _messageAvailable;
	
	public event EventHandler<ExitCode>? FailEvent;

	/// <remarks>
	/// Precondition: No specific preconditions. <br/>
	/// PostCondition: Service officially uninitialized 
	/// </remarks>
	public MessagingService()
	{
		IsServiceInitialized = false;
	}

	/// <summary>
	/// Initializes the base service - sub services (child classes) must set IsServiceInitialized=true when initialization is completed.
	/// </summary>
	/// <remarks>
	/// Precondition: Service must not be initialized <br/>
	/// Postcondition: Base service initialized - sub services (child classes) must set IsServiceInitialized=true when initialization is completed.
	/// </remarks>
	public void Initialize(Socket socket)
	{
		MessagingSocket = socket;
		_cts = new CancellationTokenSource();
		CommunicationThread = new Thread(() => Communicate(_cts.Token));
		_responses =  new ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>>();
		MessageSenderThread = new Thread(() => MessageSender(_cts.Token));
		_messageQueue = new ConcurrentQueue<Message>();
		_messageAvailable = new ManualResetEventSlim(false);
	}

	/// <summary>
	/// Returns whether the service is initialized or not.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific condition. <br/>
	/// Postcondition: Returns true if the service is initialized, false otherwise.
	/// </remarks>
	public bool IsInitialized()
	{
		return IsServiceInitialized;
	}

	/// <summary>
	/// Returns whether the socket is connected or not.
	/// </summary>
	/// <remarks>
	/// Precondition: Service should be initialized. (returns false if not) <br/>
	/// Postcondition: returns whether connected to the other side (client/server)
	/// </remarks>
	public bool IsConnected()
	{
		return IsInitialized() && MessagingSocket!.Connected;
	}

	/// <summary>
	/// Handles communication with the other entity (server/client).
	/// Specifically, handles storing responses for messages and handling request messages.
	/// This function will not exit unless the cancellation token (parameter) states that cancellation is required.
	/// After the token is canceled, the function disconnects communication and calls AfterDisconnection().
	/// This function runs on another thread, CommunicationThread.
	/// </summary>
	/// <param name="token">
	/// The cancellation token - used for determining whether the function should return or not. Cannot be null.
	/// Cancel the token (cts.Cancle()) only when communication should stop - on disconnection.
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side, (server/client) token != null. <br/>
	/// Postcondition: Communication stopped and disconnected. (socket disconnected and closed)
	/// </remarks>
	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			if (!IsConnected() && !token.IsCancellationRequested)
			{
				HandleSuddenDisconnection(token);
			}
			
			Message? message = ReceiveMessageAsync().GetAwaiter().GetResult();
			if (message == null)
			{
				if (!IsConnected() && !token.IsCancellationRequested)
				{
					HandleSuddenDisconnection(token);
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
					if (!request.IsValidMessage())
					{
						SendResponse(new MessageResponseInvalidRequestData(true, request.Id));
						break;
					}
					_ = HandleRequestAsync(request, token);
					break;
				}
				
				case MessageInfo info:
				{
					if (info.IsValidMessage())
					{
						_ = HandleInfoAsync(info, token);
					}
					break;
				}
			}
		}
		
		Disconnect();
		AfterDisconnection();
	}

	/// <summary>
	/// Runs in the MessageSenderThread. Sends messages that arrive in the message queue by the order that they arrive in.
	/// </summary>
	/// <param name="token">The cancellation token - used for stopping the thread's execution. token != null.</param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. (server/client) token != null.<br/>
	/// Postcondition: Returns when the cancellation token requires cancellation - communication is finished.
	/// </remarks>
	private void MessageSender(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			try
			{
				_messageAvailable!.Wait(token);		/* Wait until there is a message available */
				
				/* Runs until there are no messages left. message cannot be null because TryDequeue returned true. */
				while (_messageQueue!.TryDequeue(out Message? message) && !token.IsCancellationRequested)		
				{
					SendMessageOnSocketAsync(message).Wait(token);
				}

				if (_messageQueue.IsEmpty)
				{
					_messageAvailable.Reset();
				}
			}
			catch (Exception)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Sends a request to the other side (client/server) and waits for the response.
	/// </summary>
	/// <param name="message">
	/// The request to send.
	/// </param>
	/// <returns>
	/// The response and the operations' exit code. <br/>
	/// MessageResponse will be null if an error occurred, and ExitCode will hold the reason. <br/>
	/// MessageResponse is not null if the ExitCode has a success value.
	/// </returns>
	/// <remarks>
	/// Precondition: Service must be initialized and connected to the other side, message != null <br/>
	/// Postcondition: On success, returns the response for the request. <br/>
	/// On failure, the returned response is set to null and the exit code indicates the error.
	/// </remarks>
	protected async Task<(MessageResponse?, ExitCode)> SendRequestAsync(MessageRequest message)
	{
		SendMessage(message);
		
		TaskCompletionSource<MessageResponse> tcs = new TaskCompletionSource<MessageResponse>();
		_responses[message.Id] = tcs;
		
		/* Now wait for the response for this specific request that we just send */
		ExitCode result = ExitCode.Success;
		MessageResponse? response = null;
		try
		{
			response = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(SharedDefinitions.MessageTimeoutMilliseconds));
		}
		catch (TimeoutException)
		{
			result = ExitCode.MessageSendingTimeout;
		}

		if (result != ExitCode.Success || response == null)
		{
			_responses.TryRemove(message.Id, out _);
		}

		if (response != null && (!response.IsValidMessage() || response is MessageResponseInvalidRequestData))
		{
			response = null;
			result = ExitCode.InvalidMessageData;
		}
	
		return (response, result);
	}

	/// <summary>
	/// Sends a response for some request to the other side (client/server)
	/// </summary>
	/// <param name="response">
	/// The response message to send. response != null.
	/// </param>
	/// <remarks>
	/// Precondition: Service must be fully initialized and connected, response != null. <br/>
	/// Postcondition: Response sent to the other side on success. On failure, the response is not sent.
	/// </remarks>
	protected void SendResponse(MessageResponse response) => SendMessage(response);

	/// <summary>
	/// Sends an info message to the other side (client/server)
	/// </summary>
	/// <param name="info">
	/// The info message to send. info != null.
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service must be fully initialized and connected, info != null. <br/>
	/// Postcondition: Message info sent to the other side on success, exit code states success.
	/// On failure, the info message is not sent, and the exit code indicates the error.
	/// </remarks>
	protected void SendInfo(MessageInfo info) => SendMessage(info);

	/// <summary>
	/// Enqueues a message in the message queue - the message will be sent. (Basically sends the message)
	/// </summary>
	/// <param name="message">The message to send. message != null.</param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. (server/client) message != null. <br/>
	/// Postcondition: Message is in the messages queue. (And will be sent very soon)
	/// </remarks>
	private void SendMessage(Message message)
	{
		_messageQueue!.Enqueue(message);
		_messageAvailable!.Set();
	}

	/// <summary>
	/// Sends a message to the other side (client/server)
	/// </summary>
	/// <param name="message">
	/// The message to send. message != null
	/// </param>
	/// <returns>
	/// An ExitCode indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service must be fully initialized and connected to the other side. message != null. <br/>
	/// Postcondition: On success the message is fully sent, and the exit code indicates success. <br/>
	/// On failure, the message should be considered as not sent, and the exit code will indicate the error.
	/// </remarks>
	private async Task<ExitCode> SendMessageOnSocketAsync(Message message)
	{
		ExitCode result;
		
		byte[] bytes = Common.ToByteArrayWithType(message);
		byte[] sizeInBytes = BitConverter.GetBytes(bytes.Length);
		result = await SendBytesExactAsync(sizeInBytes);
		
		if (result != ExitCode.Success)
			return result;
		
		result = await SendBytesExactAsync(bytes);
		
		return result;
	}
	
	/// <summary>
	/// The initial step of handling a request. Made specifically for the Communicate() method.
	/// </summary>
	/// <param name="request">
	///     The request to process. request != null.
	/// </param>
	/// <param name="token">
	///     Used for stopping the processing of the request, as it can take time. token != null.
	/// </param>
	/// <remarks>
	/// Precondition: Caller should be the Communicate() method. <br/>
	/// Service must be initialized and connected to the other side. <br/>
	/// token != null &amp;&amp; request != null. <br/>
	/// Postcondition: The request was fully processed. <br/>
	/// If the token requests' cancellation while processing the request, <br/>
	/// this method will return and the request should be considered not handled/processed.
	/// </remarks>
	private async Task HandleRequestAsync(MessageRequest request, CancellationToken token)
	{
		try
		{
			await ProcessRequestAsync(request).WaitAsync(token);
		}
		catch (OperationCanceledException)
		{
		}
	}
	
	/// <summary>
	/// The initial step of handling an info message. Made specifically for the Communicate() method.
	/// </summary>
	/// <param name="info">The info message to process. info != null.</param>
	/// <param name="token">Used for stopping the processing of the info message, as it can take time. token != null.</param>
	/// <remarks>
	/// Precondition: Caller should be the Communicate() method. <br/>
	/// Service must be initialized and connected to the other side. <br/>
	/// token != null &amp;&amp; info != null. <br/>
	/// Postcondition: The info message was fully processed. <br/>
	/// If the token requests' cancellation while processing the info message, <br/>
	/// this method will return and the info message should be considered not handled/processed.
	/// </remarks>
	private async Task HandleInfoAsync(MessageInfo info, CancellationToken token)
	{
		try
		{
			await ProcessInfoAsync(info).WaitAsync(token);
		}
		catch (OperationCanceledException)
		{
		}
	}

	/// <summary>
	/// Waits for a message from the other side (client/server) and returns it.
	/// </summary>
	/// <returns>
	/// On success, the received message is returned. On failure, null is returned. <br/>
	/// If null is returned, it can indicate a disconnection from the other side. This should be checked with the IsConnected() method.
	/// </returns>
	/// <remarks>
	/// Precondition: The service must be fully initialized and connected to the server. <br/>
	/// Postcondition: On success, the received message is returned. On failure, null is returned.
	/// </remarks>
	private async Task<Message?> ReceiveMessageAsync()
	{
		byte[]? messageSizeInBytes = await ReceiveBytesExactAsync(4);
		if (messageSizeInBytes == null || messageSizeInBytes.Length == 0)
		{
			return null;
		}
		int size =  BitConverter.ToInt32(messageSizeInBytes, 0);	/* If the size is 0, then its an invalid message */
	
		byte[]? messageBytes = await ReceiveBytesExactAsync(size);
		if (messageBytes == null || messageBytes.Length == 0)
		{
			return null;
		}

		return (Message)Common.FromByteArrayWithType(messageBytes)!;
	}

	/// <summary>
	/// Sends an exact amount of bytes (the full byte array parameter) to the other side (client/server)
	/// </summary>
	/// <param name="bytes">
	/// The byte array to send to the other side. bytes != null
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation. On failure, the data should be considered as not sent.
	/// </returns>
	/// <remarks>
	/// Precondition: The service must be fully initialized and connected to the other side, bytes != null. <br/>
	/// Postcondition: On success, the byte array is fully sent to the other side, and an exit code of success is returned. <br/>
	/// On failure, an exit code indicating the error is returned, and the data should be considered as not sent.
	/// </remarks>
	private async Task<ExitCode> SendBytesExactAsync(byte[] bytes)
	{
		int bytesSent = 0;
		
		ExitCode result = await Task.Run(() =>
		{
			while (bytesSent < bytes.Length)
			{
				int sent;
				try
				{
					sent = MessagingSocket!.Send(bytes, bytesSent, bytes.Length - bytesSent, SocketFlags.None);
				}
				catch (Exception)
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
		});
		
		return result;
	}

	/// <summary>
	/// Waits for a specific amount of bytes to be sent to us by the other side (client/server). <br/>
	/// In other words, this method reads an exact amount of bytes from the socket, and waits until the specified amount of bytes was read.
	/// </summary>
	/// <param name="size">
	/// The amount of bytes to receive. size > 0
	/// </param>
	/// <returns>
	/// On success, the received bytes are returned. <br/>
	/// On failure, null is returned, which can indicate a disconnection. That should be checked with the IsConnected() method.
	/// </returns>
	/// <remarks>
	/// Precondition: The service must be fully initialized and connected to the other side. size != null. <br/>
	/// Postcondition: On success, the received bytes are returned. On failure, null is returned.
	/// </remarks>
	private async Task<byte[]?> ReceiveBytesExactAsync(int size)
	{
		if (size <= 0)
			return null;

		byte[]? resultBytes = await Task.Run(() =>
		{
			byte[] bytes = new byte[size];
			int bytesRead = 0;
			while (bytesRead < size)
			{
				int currentRead;
				try
				{
					currentRead = MessagingSocket!.Receive(bytes, bytesRead, size - bytesRead, SocketFlags.None);
				}
				catch (Exception)
				{
					return null;
				}

				if (currentRead <= 0) /* Means that the socket was disconnected */
				{
					return null;
				}

				bytesRead += currentRead;
			}

			return bytes;
		});
		
		return resultBytes;
	}

	/// <summary>
	/// Invokes the FailEvent event, with the specific exit code.
	/// </summary>
	/// <param name="code">
	/// The exit code to publish the event with. code != ExitCode.Success.
	/// </param>
	/// <remarks>
	/// Precondition: Failure of some kind. code != ExitCode.Success.
	/// Postcondition: FailEvent is published with the specified code.
	/// </remarks>
	protected void OnFailure(ExitCode code)
	{
		FailEvent?.Invoke(this, code);
	}

	/// <summary>
	/// Processes requests from the other side. (client/server) <br/>
	/// Children of this class can override this method to implement request handling.
	/// </summary>
	/// <param name="request">
	/// The sent request that should be processed. request != null.
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. <br/>
	/// A message of request type was received - should be processed. request != null. <br/>
	/// Postcondition: Request is considered processed.
	/// </remarks>
	protected virtual async Task ProcessRequestAsync(MessageRequest request)
	{
	}
	
	/// <summary>
	/// Processes message info from the other side. (client/server) <br/>
	/// Children of this class can override this method to implement message info handling.
	/// </summary>
	/// <param name="info">
	/// The sent message info that should be processed. info != null.
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. <br/>
	/// A message of info type was received - should be processed. info != null. <br/>
	/// Postcondition: Info is considered processed.
	/// </remarks>
	protected virtual async Task ProcessInfoAsync(MessageInfo info)
	{
	}
	
	/// <summary>
	/// Disconnects from the other side (client/server), cancels the communication thread, and uninitializes the service.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. <br/>
	/// Postcondition: Service disconnected from the other side, communication thread dead, service uninitialized.
	/// </remarks>
	public virtual void Disconnect()
	{
		if (_cts != null)
		{
			_cts.Cancel();
		}

		if (MessagingSocket != null)
		{
			MessagingSocket.Dispose();
			MessagingSocket.Close();
		}
		
		if (CommunicationThread != null && Thread.CurrentThread != CommunicationThread && CommunicationThread.IsAlive)
		{
			CommunicationThread.Join();
		}
		
		if (_cts != null)
		{
			_cts.Dispose();
			_cts = null;
		}

		IsServiceInitialized = false;
		
		AfterDisconnection();
	}
	
	/// <summary>
	/// Handles a sudden disconnection from the other side. (client/server) <br/>
	/// Child classes of this class can override this method to handle disconnections accordingly.
	/// </summary>
	/// <param name="token">
	/// Optional parameter, can be null. <br/>
	/// If child classes override this method, they may use the token to cancel a long-running operation. (Trying to reconnect for example)
	/// </param>
	/// <remarks>
	/// Precondition: A sudden disconnection from the other side has happened. The cancellation token can be set to null - its optional.
	/// Postcondition: The sudden disconnection has been handled accordingly.
	/// </remarks>
	protected virtual void HandleSuddenDisconnection(CancellationToken? token = null)
	{
		OnFailure(ExitCode.DisconnectedFromServer);
		AfterDisconnection();
	}
	
	/// <summary>
	/// Runs after a disconnection. (Both regular disconnections and sudden disconnections)
	/// </summary>
	/// <remarks>
	/// Precondition: A disconnection from the other side has happened, regular or sudden disconnection.
	/// Postcondition: The disconnection is considered as handled.
	/// </remarks>
	protected virtual void AfterDisconnection()
	{
	}
}
