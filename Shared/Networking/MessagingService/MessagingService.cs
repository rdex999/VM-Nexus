using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Shared.Networking;

public partial class MessagingService
{
	public event EventHandler<ExitCode>? FailEvent;
	protected Socket? TcpSocket;
	protected Socket? UdpSocket;
	protected WebSocket? WebSocket;
	protected SslStream? TcpSslStream;
	protected readonly CancellationTokenSource Cts;
	protected bool IsServiceInitialized;
	private readonly ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>> _responses;
	private readonly Thread _messageUdpSenderThread;
	private readonly ConcurrentQueue<Message> _messageTcpQueue;
	private readonly ConcurrentQueue<Message> _messageUdpQueue;
	private readonly ManualResetEventSlim _messageTcpAvailable;
	private readonly ManualResetEventSlim _messageUdpAvailable;
	protected bool IsUdpMessagingRunning = false;
	private static readonly byte[] MessageMagic = Encoding.ASCII.GetBytes("VMNX");
	private readonly Dictionary<Guid, IncomingMessageUdp> _incomingUdpMessages;
	private readonly ConcurrentDictionary<Guid, TransferHandler> _ongoingTransfers;
	protected readonly TransferRateLimiter TransferLimiter;
	private UdpCryptoService? CryptoService;
	private const int DatagramSize = 1200;

	/// <remarks>
	/// Precondition: No specific preconditions. <br/>
	/// PostCondition: Service officially uninitialized 
	/// </remarks>
	public MessagingService()
	{
		IsServiceInitialized = false;
		
		Cts = new CancellationTokenSource();
		_messageUdpSenderThread = new Thread(MessageUdpSender);
		_responses = new ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>>();
		_messageTcpQueue = new ConcurrentQueue<Message>();
		_messageUdpQueue = new ConcurrentQueue<Message>();
		_messageTcpAvailable = new ManualResetEventSlim(false);
		_messageUdpAvailable = new ManualResetEventSlim(false);
		_incomingUdpMessages = new Dictionary<Guid, IncomingMessageUdp>();
		_ongoingTransfers = new ConcurrentDictionary<Guid, TransferHandler>();
		TransferLimiter = new TransferRateLimiter();
	}

	/// <summary>
	/// Starts the message sender and receiver TCP task and thread. Call only after both TcpSocket and UdpSocket have been initialized.
	/// </summary>
	/// <remarks>
	/// Precondition: TcpSocket and UdpSocket have been initialized. <br/>
	/// Postcondition: Message receiver and sender TCP task and thread are started.
	/// Sending and receiving TCP messages is possible after calling this method.
	/// </remarks>
	protected void StartTcp()
	{
		_ = MessageTcpReceiverAsync();
		_ = MessageTcpSenderAsync();
	}
	
	/// <summary>
	/// Starts the message sender and receiver UDP task and thread. Call only after both TcpSocket and UdpSocket have been initialized.
	/// </summary>
	/// <remarks>
	/// Precondition: TcpSocket and UdpSocket have been initialized. <br/>
	/// Postcondition: Message receiver and sender UDP task and thread are started.
	/// Sending and receiving UDP messages is possible after calling this method.
	/// </remarks>
	protected void StartUdp()
	{
		_ = MessageUdpReceiverAsync();
		
		if (!_messageUdpSenderThread.IsAlive)
			_messageUdpSenderThread.Start();

		IsUdpMessagingRunning = true;
	}

	/// <summary>
	/// Reset the UDP crypto service. Re-generate key and salt, reset counters.
	/// </summary>
	/// <param name="key32">The new key to use. key32 != null.</param>
	/// <param name="salt4">The new salt to use. salt != null.</param>
	/// <remarks>
	/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
	/// Postcondition: Service is reset, the given key and salt are now used.
	/// </remarks>
	protected void ResetUdpCrypto(byte[] key32, byte[] salt4)
	{
		if (CryptoService == null)
			CryptoService = new UdpCryptoService(key32, salt4);
		else
			CryptoService.Reset(key32, salt4);
	}
	
	/// <summary>
	/// Reset the UDP crypto service. Re-generate key and salt, reset counters.
	/// </summary>
	/// <param name="key32">The new generated key output. key32 != null.</param>
	/// <param name="salt4">The new generated salt output. salt != null.</param>
	/// <remarks>
	/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
	/// Postcondition: Service is reset, the new key and salt are written into the given outputs.
	/// </remarks>
	protected void ResetUdpCrypto(out byte[] key32, out byte[] salt4)
	{
		if (CryptoService == null)
			CryptoService = new UdpCryptoService(out key32, out salt4);
		else
			CryptoService.Reset(out key32, out salt4);
	}

	/// <summary>
	/// Returns whether the service is initialized or not.
	/// </summary>
	/// <remarks>
	/// Precondition: No specific condition. <br/>
	/// Postcondition: Returns true if the service is initialized, false otherwise.
	/// </remarks>
	public bool IsInitialized() => IsServiceInitialized;

	/// <summary>
	/// Returns whether the socket is connected or not.
	/// </summary>
	/// <remarks>
	/// Precondition: Service should be initialized. (returns false if not) <br/>
	/// Postcondition: returns whether connected to the other side (client/server)
	/// </remarks>
	public bool IsConnected() =>
		IsInitialized() && ((TcpSocket != null && TcpSocket!.Connected) || (WebSocket != null && WebSocket.State == WebSocketState.Open));

	/// <summary>
	/// Handles communication with the other entity (server/client).
	/// Specifically, handles storing responses for messages and handling request messages.
	/// This function will not exit unless the cancellation token (parameter) states that cancellation is required.
	/// After the token is canceled, the function disconnects communication and calls AfterDisconnection().
	/// This function runs on another thread, CommunicationThread.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side, (server/client) token != null. <br/>
	/// Postcondition: Communication stopped and disconnected. (socket disconnected and closed)
	/// </remarks>
	private async Task MessageTcpReceiverAsync()
	{
		while (!Cts.Token.IsCancellationRequested)
		{
			if (!IsConnected() && !Cts.Token.IsCancellationRequested)
			{
				try
				{
					await Task.Run(() => HandleSuddenDisconnection(), Cts.Token);
				}
				catch (Exception)
				{
					return;
				}
			}

			Message? message;
			try
			{
				message = await ReceiveMessageTcpAsync().WaitAsync(Cts.Token).ConfigureAwait(false);
			}
			catch (Exception)
			{
				return;
			}
			
			if (message == null)
			{
				try
				{
					await Task.Delay(50, Cts.Token).ConfigureAwait(false);
				}
				catch (Exception)
				{
					return;
				}
				continue;	
			}

			await ProcessMessageAsync(message);
		}
		
		Disconnect();
		AfterDisconnection();
	}

	/// <summary>
	/// Receives UDP messages from the other side, and processes them.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. This method should be started only from the Start() method. <br/>
	/// Postcondition: While running, receives and processes UDP messages from the other side. Returns when the given cancellation token requires cancellation.
	/// </remarks>
	private async Task MessageUdpReceiverAsync()
	{
		byte[] buffer = new byte[DatagramSize];
		while (!Cts.Token.IsCancellationRequested)
		{
			int size;
			try
			{
				size = await UdpSocket!.ReceiveAsync(buffer, Cts.Token).ConfigureAwait(false);
			}
			catch (Exception)
			{
				continue;
			}
			
			if (!UdpPacket.IsValidPacket(buffer) || CryptoService == null)
				continue;

			UdpPacket packet = new UdpPacket(buffer, size);
			
			ExitCode result;
			Message? message;
			if (_incomingUdpMessages.TryGetValue(packet.MessageId, out IncomingMessageUdp? incoming))
			{
				/* Checking here before decrypting to save decryption time. We might detect an invalid packet before its decrypted. */
				if (incoming.CanReceivePacket(packet) == ExitCode.InvalidUdpPacket)
				{
					_incomingUdpMessages.Remove(packet.MessageId);
					continue;
				}
			}

			UdpPacket? decrypted = CryptoService.Decrypt(packet);
			if (decrypted == null)
				continue;

			if (incoming == null)
				_incomingUdpMessages.TryAdd(decrypted.MessageId, new IncomingMessageUdp(decrypted, OnIncomingMessageTimeout, out result, out message));
			else
				result = incoming.ReceivePacket(decrypted, out message);
			
			switch (result)
			{
				case ExitCode.Success:
					await ProcessMessageAsync(message!);
					_incomingUdpMessages.Remove(decrypted.MessageId);
					break;
					
				case ExitCode.InvalidUdpPacket or ExitCode.MessageUdpCorrupted:
					_incomingUdpMessages.Remove(decrypted.MessageId);
					break;
			}
		}
	}

	/// <summary>
	/// Handles a timeout for an incoming message. Removes the message and frees resources.
	/// </summary>
	/// <param name="messageId">The ID of the message that has timed-out. messageId != null.</param>
	/// <remarks>
	/// Precondition: An incoming message has timed-out. messageId != null. <br/>
	/// Postcondition: The message is removed and used resources are freed.
	/// </remarks>
	private void OnIncomingMessageTimeout(Guid messageId) =>
		_incomingUdpMessages.Remove(messageId);

	/// <summary>
	/// Runs in the MessageSenderThread. Sends messages that arrive in the message queue by the order that they arrive in.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. (server/client) This method should only be started from the StartTcp() method.<br/>
	/// Postcondition: Returns when the cancellation token requires cancellation - communication is finished.
	/// </remarks>
	private async Task MessageTcpSenderAsync()
	{
		while (!Cts.Token.IsCancellationRequested)
		{
			try
			{
				if (OperatingSystem.IsBrowser())
					while (!_messageTcpAvailable.IsSet)
						await Task.Delay(50, Cts.Token);
				
				else
					await Task.Run(() => _messageTcpAvailable.Wait(Cts.Token));		/* Wait until there is a message available */
				
				/* Runs until there are no messages left. message cannot be null because TryDequeue returned true. */
				while (_messageTcpQueue.TryDequeue(out Message? message) && !Cts.Token.IsCancellationRequested)		
				{
					await SendMessageTcpOnSocketAsync(message);
				}

				if (_messageTcpQueue.IsEmpty)
				{
					_messageTcpAvailable.Reset();
				}
			}
			catch (Exception)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Sends UDP messages from the UDP message queue to the other side.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. This method should be started only from the StartUdp() method. <br/>
	/// Postcondition: While running, receives and processes UDP messages from the other side. Returns when the given cancellation token requires cancellation.
	/// </remarks>
	private void MessageUdpSender()
	{
		while (!Cts.Token.IsCancellationRequested)
		{
			try
			{
				_messageUdpAvailable.Wait(Cts.Token);
			}
			catch (Exception)
			{
				continue;
			}

			if (CryptoService == null)
			{
				_messageUdpQueue.Clear();
				continue;
			}

			while (_messageUdpQueue.TryDequeue(out Message? message))
			{
				byte[] messageBytes = Common.ToByteArrayWithType(message);
				int bytesSent = 0;
				while (bytesSent < messageBytes.Length && !Cts.Token.IsCancellationRequested)
				{
					UdpPacket? packet = CryptoService.Encrypt(
						message.Id,
						messageBytes.Length,
						bytesSent,
						messageBytes.AsSpan(bytesSent, Math.Min(UdpPacket.MaxPayloadSize, messageBytes.Length - bytesSent))
					);

					if (packet == null)
						continue;

					int sent;
					try
					{
						sent = UdpSocket!.Send(packet.Packet);
					}
					catch (Exception)
					{
						break;
					}

					bytesSent += Math.Max(0, sent - UdpPacket.HeaderSize);
				}
			}
			
			if (_messageUdpQueue.IsEmpty)
				_messageUdpAvailable.Reset();
		}
	}
	
	/// <summary>
	/// Processes the message, redirects it to an appropriate handler.
	/// </summary>
	/// <param name="message">The received message. message != null.</param>
	/// <remarks>
	/// Precondition: A message was received. message != null. <br/>
	/// Postcondition: Message is redirected to appropriate handler. Handler is handling the message.
	/// In the case of a response, it is registered as a response for a request that was sent. (if any)
	/// </remarks>
	private async Task ProcessMessageAsync(Message message)
	{
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
				_ = HandleRequestAsync(request);
				break;
			}
			case MessageInfoTransferData transferData:
			{
				if (message.IsValidMessage())
					await HandleInfoAsync(transferData);
				
				break;
			}
			case MessageInfoTcp:
			case MessageInfoUdp:
			{
				if (message.IsValidMessage())
					_ = HandleInfoAsync(message);
				
				break;
			}
		}
	}

	/// <summary>
	/// Adds the given started download to the incoming downloads.
	/// </summary>
	/// <param name="handler">The started download. handler != null.</param>
	/// <remarks>
	/// Precondition: The given download handler was started. handler != null. <br/>
	/// Postcondition: Download is added to the incoming downloads and will receive data.
	/// </remarks>
	protected void AddTransfer(TransferHandler handler)
	{
		if (_ongoingTransfers.ContainsKey(handler.Id))
			return;
		
		_ongoingTransfers.TryAdd(handler.Id, handler);
		handler.Ended += OnTransferEnded;
	}

	/// <summary>
	/// Create a download ID for a new incoming download.
	/// </summary>
	/// <returns>A unique ID for the new incoming download.</returns>
	/// <remarks>
	/// Precondition: Allocating a unique ID for a new download is required. <br/>
	/// Postcondition: A unique guid for the new download is returned.
	/// </remarks>
	protected Guid CreateTransferId()
	{
		Guid id;
		do
			id = Guid.NewGuid();
		while (_ongoingTransfers.ContainsKey(id));

		return id;
	}

	/// <summary>
	/// Handles both download completed and failed events. Removes the download from the incoming downloads.
	/// </summary>
	/// <param name="sender">The download that has ended. sender != null &amp;&amp; sender is DownloadHandler.</param>
	/// <param name="e">Unused.</param>
	/// <remarks>
	/// Precondition: A download has either completed or failed. sender != null &amp;&amp; sender is DownloadHandler. <br/>
	/// Postcondition: The download is removed from the incoming downloads.
	/// </remarks>
	private void OnTransferEnded(object? sender, EventArgs e)
	{
		if (sender == null || sender is not TransferHandler handler)
			return;
		
		_ongoingTransfers.TryRemove(handler.Id, out _);
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
	/// The info message to send. info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp)
	/// </param>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Service must be fully initialized and connected, info != null. <br/>
	/// Postcondition: Message info sent to the other side on success, exit code states success.
	/// On failure, the info message is not sent, and the exit code indicates the error.
	/// </remarks>
	protected void SendInfo(Message info) => SendMessage(info);

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
		if (!IsConnected())
			return;
		
		if (message is MessageTcp || UdpSocket == null)
		{
			_messageTcpQueue.Enqueue(message);
			_messageTcpAvailable.Set();
		}
		else if (message is MessageUdp)
		{
			_messageUdpQueue.Enqueue(message);
			_messageUdpAvailable.Set();
		}
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
	private async Task<ExitCode> SendMessageTcpOnSocketAsync(Message message)
	{
		ExitCode result;
		Stopwatch stopwatch = Stopwatch.StartNew();
		
		byte[] bytes = Common.ToByteArrayWithType(message);
		byte[] sizeInBytes = BitConverter.GetBytes(bytes.Length);

		result = await SendBytesExactTcpAsync(sizeInBytes);
		
		if (result != ExitCode.Success)
			return result;
		
		result = await SendBytesExactTcpAsync(bytes);

		stopwatch.Stop();
		long sent = bytes.Length + 4;
		TransferLimiter.SetRateBps((long)(sent / stopwatch.Elapsed.TotalSeconds));
		
		return result;
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
	private async Task<Message?> ReceiveMessageTcpAsync()
	{
		byte[]? messageSizeInBytes = await ReceiveBytesExactTcpAsync(4);
		if (messageSizeInBytes == null || messageSizeInBytes.Length == 0)
		{
			return null;
		}
		int size =  BitConverter.ToInt32(messageSizeInBytes, 0);	/* If the size is 0, then its an invalid message */
	
		byte[]? messageBytes = await ReceiveBytesExactTcpAsync(size);
		if (messageBytes == null || messageBytes.Length == 0)
		{
			return null;
		}

		return (Message)Common.FromByteArrayWithType(messageBytes)!;
	}
	
	/// <summary>
	/// The initial step of handling a request. Made specifically for the Communicate() method.
	/// </summary>
	/// <param name="request">
	///     The request to process. request != null.
	/// </param>
	/// <remarks>
	/// Precondition: Caller should be the Communicate() method. <br/>
	/// Service must be initialized and connected to the other side. <br/>
	/// request != null. <br/>
	/// Postcondition: The request was fully processed. <br/>
	/// If the token requests' cancellation while processing the request, <br/>
	/// this method will return and the request should be considered not handled/processed.
	/// </remarks>
	private async Task HandleRequestAsync(MessageRequest request)
	{
		try
		{
			await ProcessRequestAsync(request).WaitAsync(Cts.Token);
		}
		catch (OperationCanceledException)
		{
		}
	}
	
	/// <summary>
	/// The initial step of handling an info message. Made specifically for the Communicate() method.
	/// </summary>
	/// <param name="info">The info message to process. info != null.</param>
	/// <remarks>
	/// Precondition: Caller should be the Communicate() method. <br/>
	/// Service must be initialized and connected to the other side. <br/>
	/// info != null. <br/>
	/// Postcondition: The info message was fully processed. <br/>
	/// If the token requests' cancellation while processing the info message, <br/>
	/// this method will return and the info message should be considered not handled/processed.
	/// </remarks>
	private async Task HandleInfoAsync(Message info)
	{
		try
		{
			await ProcessInfoAsync(info).WaitAsync(Cts.Token);
		}
		catch (OperationCanceledException)
		{
		}
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
	/// Precondition: The service must be fully initialized and connected to the other side, socket connected, bytes != null. <br/>
	/// Postcondition: On success, the byte array is fully sent to the other side, and an exit code of success is returned. <br/>
	/// On failure, an exit code indicating the error is returned, and the data should be considered as not sent.
	/// </remarks>
	private async Task<ExitCode> SendBytesExactTcpAsync(byte[] bytes)
	{
		ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(bytes, 0, bytes.Length);

		try
		{
			if (TcpSslStream != null)
				await TcpSslStream!.WriteAsync(memory, Cts.Token);

			else if (WebSocket != null)
				await WebSocket.SendAsync(memory, WebSocketMessageType.Binary, true, Cts.Token);
			
			else
				return ExitCode.DisconnectedFromServer;
		}
		catch (Exception)
		{
			return ExitCode.DisconnectedFromServer;
		}

		return ExitCode.Success;
	}

	/// <summary>
	/// Waits for a specific amount of bytes to be sent to us by the other side (client/server). <br/>
	/// In other words, this method reads an exact amount of bytes from the socket, and waits until the specified amount of bytes was read.
	/// </summary>
	/// <param name="size">The amount of bytes to receive. size > 0.</param>
	/// <returns>
	/// On success, the received bytes are returned. <br/>
	/// On failure, null is returned, which can indicate a disconnection. That should be checked with the IsConnected() method.
	/// </returns>
	/// <remarks>
	/// Precondition: The service must be fully initialized and connected to the other side. size > 0. <br/>
	/// Postcondition: On success, the received bytes are returned. On failure, null is returned.
	/// </remarks>
	private async Task<byte[]?> ReceiveBytesExactTcpAsync(int size)
	{
		if (size <= 0)
			return null;

		byte[] bytes = new byte[size];
		int bytesRead = 0;
		while (bytesRead < size)
		{
			Memory<byte> memory = new Memory<byte>(bytes, bytesRead, size - bytesRead);
			
			int currentRead;
			try
			{
				if (TcpSslStream != null)
					currentRead = await TcpSslStream!.ReadAsync(memory, Cts.Token).ConfigureAwait(false);

				else if (WebSocket != null)
				{
					ValueWebSocketReceiveResult result =
						await WebSocket!.ReceiveAsync(memory, Cts.Token).ConfigureAwait(false);
					currentRead = result.Count;
				}

				else
					return null;
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
	/// The sent message info that should be processed. info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp).
	/// </param>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. <br/>
	/// A message of info type was received - should be processed. info != null &amp;&amp; (info is MessageInfoTcp || info is MessageInfoUdp) <br/>
	/// Postcondition: Info is considered processed.
	/// </remarks>
	protected virtual async Task ProcessInfoAsync(Message info)
	{
		switch (info)
		{
			case MessageInfoCryptoUdp cryptoInfo:
			{
				ResetUdpCrypto(cryptoInfo.MasterKey32, cryptoInfo.Salt4);
				break;
			}
			case MessageInfoTransferData downloadData:
			{
				if (!_ongoingTransfers.TryGetValue(downloadData.StreamId, out TransferHandler? handler))
					return;

				await handler.ReceiveAsync(downloadData);
			
				if (!handler.IsDownloading)	
					OnTransferEnded(handler, EventArgs.Empty);
				
				break;
			}
		}
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
		try
		{
			Cts.Cancel();
		}
		catch (Exception)
		{
			return;
		}

		TcpSslStream?.Dispose();

		if (UdpSocket != null)
		{
			UdpSocket.Close();
			UdpSocket.Dispose();
		}

		if (Thread.CurrentThread != _messageUdpSenderThread && _messageUdpSenderThread.IsAlive) 
			_messageUdpSenderThread.Join();

		Cts.Dispose();
		CryptoService?.Dispose();
		
		IsServiceInitialized = false;
		
		AfterDisconnection();
	}
	
	/// <summary>
	/// Handles a sudden disconnection from the other side. (client/server) <br/>
	/// Child classes of this class can override this method to handle disconnections accordingly.
	/// </summary>
	/// <remarks>
	/// Precondition: A sudden disconnection from the other side has happened.
	/// Postcondition: The sudden disconnection has been handled accordingly.
	/// </remarks>
	protected virtual void HandleSuddenDisconnection()
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