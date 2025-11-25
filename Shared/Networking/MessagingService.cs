using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace Shared.Networking;

public class MessagingService
{
	public event EventHandler<ExitCode>? FailEvent;
	protected Socket? TcpSocket;
	protected Socket? UdpSocket;
	protected readonly CancellationTokenSource Cts;
	protected bool IsServiceInitialized;
	private readonly ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>> _responses;
	private readonly Thread _messageTcpSenderThread;
	private readonly Thread _messageUdpSenderThread;
	private readonly ConcurrentQueue<Message> _messageTcpQueue;
	private readonly ConcurrentQueue<Message> _messageUdpQueue;
	private readonly ManualResetEventSlim _messageTcpAvailable;
	private readonly ManualResetEventSlim _messageUdpAvailable;
	private static readonly byte[] MessageMagic = Encoding.ASCII.GetBytes("VMNX");
	private readonly Dictionary<Guid, IncomingMessageUdp> _incomingUdpMessages;
	private const int DatagramSize = 1200;

	private struct UdpPacket
	{
		public const int HeaderSize = 4 + 16 + 4 + 4 + 4;
		public const int MaxPayloadSize = DatagramSize - HeaderSize;
		public Guid MessageId { get; }
		public int MessageSize { get; }
		public int PayloadSize { get; }
		public int Offset { get; }
		public ReadOnlySpan<byte> Payload => _packet.AsSpan(HeaderSize, PayloadSize);
		public ReadOnlySpan<byte> Packet => _packet.AsSpan();
		private readonly byte[] _packet;
		
		public UdpPacket(byte[] packet)
		{
			int nextField = 0;
			_packet = packet;
			
			nextField += MessageMagic.Length;
			
			MessageId = new Guid(packet.AsSpan(nextField, 16));
			nextField += 16;
		
			MessageSize = BitConverter.ToInt32(_packet.AsSpan(nextField, sizeof(int)));
			nextField += sizeof(int);
			
			PayloadSize = BitConverter.ToInt32(_packet.AsSpan(nextField, sizeof(int)));
			nextField += sizeof(int);
			
			Offset = BitConverter.ToInt32(_packet.AsSpan(nextField, sizeof(int)));
		}

		/// <summary>
		/// Instantiates a packet from the given data. (Packet content)
		/// </summary>
		/// <param name="messageId">The ID of the message that this packet is a part of. messageId != null.</param>
		/// <param name="messageSize">The size of the entier message, in bytes. messageSize > 0.</param>
		/// <param name="offset">The offset of this packet's payload in the message. offset >= 0.</param>
		/// <param name="payload">
		/// The payload, the content of this packet. payload != null &amp;&amp; payload.Length > 0 &amp;&amp; payload.Lenght <= DatagramSize - HeaderSize.
		/// </param>
		/// <returns>A byte array representing the packet with the given data.</returns>
		/// <remarks>
		/// Precondition: messageId != null &amp;&amp; messageSize > 0 &amp;&amp; offset >= 0
		/// &amp;&amp; payload != null &amp;&amp; payload.Length > 0 &amp;&amp; payload.Lenght <= DatagramSize - HeaderSize. <br/>
		/// Postcondition: A byte array representing the packet with the given data is returned.
		/// </remarks>
		public UdpPacket(Guid messageId, int messageSize, int offset, ReadOnlySpan<byte> payload)
		{
			_packet = new byte[HeaderSize + payload.Length];
			int nextField = 0;
		
			MessageMagic.CopyTo(_packet, nextField);
			nextField += MessageMagic.Length;
			
			messageId.ToByteArray().CopyTo(_packet, nextField);
			nextField += 16;
			
			BitConverter.GetBytes(messageSize).CopyTo(_packet, nextField);
			nextField += sizeof(int);
			
			BitConverter.GetBytes(payload.Length).CopyTo(_packet, nextField);
			nextField += sizeof(int);
			
			BitConverter.GetBytes(offset).CopyTo(_packet, nextField);
			nextField += sizeof(int);
			
			payload.CopyTo(_packet.AsSpan(nextField));
		}
		
		/// <summary>
		/// Checks whether the given UDP packet is valid or not. (Checks magic and payload size)
		/// </summary>
		/// <param name="packet">The UDP packet. packet != null.</param>
		/// <returns>True if the UDP packet is valid, false otherwise.</returns>
		/// <remarks>
		/// This method should be called before constructing a UdpPacket instance. <br/>
		/// Precondition: A UDP packet was received, and validating it is required. packet != null. <br/>
		/// Postcondition: Returns true if the UDP packet is valid, false otherwise.
		/// </remarks>
		public static bool IsValidPacket(byte[] packet)
		{
			if (packet.Length > DatagramSize || packet.Length < HeaderSize)
				return false;
			
			bool validMagic = packet.AsSpan(0, MessageMagic.Length).SequenceEqual(MessageMagic);
			if (!validMagic)
				return false;
			
			int payloadSize = BitConverter.ToInt32(packet.AsSpan(24, sizeof(int)));
			
			return payloadSize <= DatagramSize - HeaderSize;
		}

	}
	
	private class IncomingMessageUdp
	{
		public Guid MessageId { get; }
		private readonly byte[] _data;
		private int _bytesReceived = 0;
		private readonly BitArray _chunks;

		/// <summary>
		/// Creates this incoming message and loads the first packet using ReceivePacket().
		/// </summary>
		/// <param name="firstPacket">The first packet that was received. firstPacket != null.</param>
		/// <param name="result">An exit code indicating the result of receiving the first packet. (See ReceivePacket() documentation.)</param>
		/// <param name="message">If this was the last packet, the output message is written to this pointer. Null is written otherwise.</param>
		/// <remarks>
		/// Precondition: A packet was received and no instance of IncomingMessageUdp exists for it. firstPacket != null. <br/>
		/// Postcondition: This IncomingMessageUdp is created. As for the result and message parameters, see documentation of ReceivePacket().
		/// </remarks>
		public IncomingMessageUdp(UdpPacket firstPacket, out ExitCode result, out Message? message)
		{
			_data = new byte[firstPacket.MessageSize];
			int chunks = (_data.Length + UdpPacket.MaxPayloadSize - 1) / UdpPacket.MaxPayloadSize;		/* messageSize / MaxPayloadSize (round up remainder) */
			_chunks = new BitArray(chunks);
			MessageId = firstPacket.MessageId;

			result = ReceivePacket(firstPacket, out message);
		}

		/// <summary>
		/// Receives the packet to form the message. Can output the message.
		/// </summary>
		/// <param name="packet">The packet to receive into this message. packet must be valid. packet != null.</param>
		/// <param name="message">If this was the last packet, the output message is written to this pointer. Null is written otherwise.</param>
		/// <returns>
		/// An exit code indicating the result. (packet invalid, more packets to come, success.) <br/>
		/// If success is returned, the message parameter contains the final message.
		/// </returns>
		/// <remarks>
		/// Precondition: A packet was received. packet must be valid. packet != null. <br/>
		/// Postcondition: Multiple cases, result depends on the returned exit code: <br/>
		///	- 1: Success				- This was the last packet in the message. The message parameter contains the received message. (message != null) <br/>
		///	- 2: InvalidUdpPacket		- This packet is invalid and is not written into the message. Caller should remove this message as it will never be completed. <br/>
		///	- 3: UdpPacketDuplicate		- This packet was already received. The payload is not written once again. <br/>
		/// - 4: MessageUdpCorrupted	- This was the last packet in the message, but one or more of the packets were corrupted which means that the whole message
		///		is corrupted and thus cannot be formed. (The message parameter contains null) The caller should remove this message.<br/>
		/// - 5: MessageUdpNotCompleted	- There are more packets to come. 
		/// </remarks>
		public ExitCode ReceivePacket(UdpPacket packet, out Message? message)
		{
			message = null;
			
			if (packet.MessageSize != _data.Length)
				return ExitCode.InvalidUdpPacket;
			
			int chunk = packet.Offset / UdpPacket.MaxPayloadSize;
			if (chunk >= _chunks.Length)
				return ExitCode.InvalidUdpPacket;

			if (_chunks[chunk])
				return ExitCode.UdpPacketDuplicate;

			int chunkSize;
			if (chunk == _chunks.Length - 1)
				chunkSize = _data.Length % UdpPacket.MaxPayloadSize;
			else
				chunkSize = UdpPacket.MaxPayloadSize;
				
			if (packet.PayloadSize > chunkSize)
				return ExitCode.InvalidUdpPacket;
			
			packet.Payload.CopyTo(_data.AsSpan(packet.Offset));
			_chunks[chunk] = true;
			_bytesReceived += chunkSize;

			if (_bytesReceived >= packet.MessageSize)
			{
				Message? msg = (Message?)Common.FromByteArrayWithType(_data);
				if (msg == null)
					return ExitCode.MessageUdpCorrupted;

				message = msg;
				return ExitCode.Success;
			}

			return ExitCode.MessageUdpNotCompleted;
		}
	}

	/// <remarks>
	/// Precondition: No specific preconditions. <br/>
	/// PostCondition: Service officially uninitialized 
	/// </remarks>
	public MessagingService()
	{
		IsServiceInitialized = false;
		
		Cts = new CancellationTokenSource();
		_messageTcpSenderThread = new Thread(MessageTcpSender);
		_messageUdpSenderThread = new Thread(MessageUdpSender);
		_responses = new ConcurrentDictionary<Guid, TaskCompletionSource<MessageResponse>>();
		_messageTcpQueue = new ConcurrentQueue<Message>();
		_messageUdpQueue = new ConcurrentQueue<Message>();
		_messageTcpAvailable = new ManualResetEventSlim(false);
		_messageUdpAvailable = new ManualResetEventSlim(false);
		_incomingUdpMessages = new Dictionary<Guid, IncomingMessageUdp>();
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
		
		if (!_messageTcpSenderThread.IsAlive)
			_messageTcpSenderThread.Start();
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
		return IsInitialized() && TcpSocket != null && TcpSocket!.Connected;
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

			ProcessMessage(message);
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
			try
			{
				await UdpSocket!.ReceiveAsync(buffer, Cts.Token).ConfigureAwait(false);
			}
			catch (Exception)
			{
				continue;
			}
			
			if (!UdpPacket.IsValidPacket(buffer))
				continue;

			UdpPacket packet = new UdpPacket(buffer);
			
			ExitCode result;
			Message? message;
			if (_incomingUdpMessages.TryGetValue(packet.MessageId, out IncomingMessageUdp? incoming))
			{
				result = incoming.ReceivePacket(packet, out message);
			}
			else
			{
				_incomingUdpMessages.TryAdd(packet.MessageId, new IncomingMessageUdp(packet, out result, out message));
			}

			switch (result)
			{
				case ExitCode.Success:
					ProcessMessage(message!);
					_incomingUdpMessages.Remove(packet.MessageId);
					break;
				
				case ExitCode.InvalidUdpPacket or ExitCode.MessageUdpCorrupted:
					_incomingUdpMessages.Remove(packet.MessageId);
					break;
			}
		}
	}
	
	/// <summary>
	/// Runs in the MessageSenderThread. Sends messages that arrive in the message queue by the order that they arrive in.
	/// </summary>
	/// <remarks>
	/// Precondition: Service fully initialized and connected to the other side. (server/client) This method should only be started from the StartTcp() method.<br/>
	/// Postcondition: Returns when the cancellation token requires cancellation - communication is finished.
	/// </remarks>
	private void MessageTcpSender()
	{
		while (!Cts.Token.IsCancellationRequested)
		{
			try
			{
				_messageTcpAvailable.Wait(Cts.Token);		/* Wait until there is a message available */
				
				/* Runs until there are no messages left. message cannot be null because TryDequeue returned true. */
				while (_messageTcpQueue.TryDequeue(out Message? message) && !Cts.Token.IsCancellationRequested)		
				{
					SendMessageTcpOnSocketAsync(message).Wait(Cts.Token);
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

			while (_messageUdpQueue.TryDequeue(out Message? message))
			{
				byte[] messageBytes = Common.ToByteArrayWithType(message);
				int bytesSent = 0;
				while (bytesSent < messageBytes.Length && !Cts.Token.IsCancellationRequested)
				{
					UdpPacket packet = new UdpPacket(
						message.Id,
						messageBytes.Length,
						bytesSent,
						messageBytes.AsSpan(bytesSent, Math.Min(UdpPacket.MaxPayloadSize, messageBytes.Length - bytesSent))
					);

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
	private void ProcessMessage(Message message)
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
				
			case MessageInfoTcp:
			case MessageInfoUdp:
			{
				if (message.IsValidMessage())
				{
					_ = HandleInfoAsync(message);
				}
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
		if (message is MessageTcp)
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
		
		byte[] bytes = Common.ToByteArrayWithType(message);
		byte[] sizeInBytes = BitConverter.GetBytes(bytes.Length);

		result = await SendBytesExactTcpAsync(sizeInBytes);
		
		if (result != ExitCode.Success)
			return result;
		
		result = await SendBytesExactTcpAsync(bytes);
		
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
		int bytesSent = 0;
		
		while (bytesSent < bytes.Length)
		{
			ReadOnlyMemory<byte> memory = new ReadOnlyMemory<byte>(bytes, bytesSent, bytes.Length - bytesSent);
		
			int sent;
			try
			{
				sent = await TcpSocket!.SendAsync(memory);
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
				currentRead = await TcpSocket!.ReceiveAsync(memory);
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

		if (TcpSocket != null)
		{
			TcpSocket.Close();
			TcpSocket.Dispose();
		}

		if (UdpSocket != null)
		{
			UdpSocket.Close();
			UdpSocket.Dispose();
		}

		if (Thread.CurrentThread != _messageTcpSenderThread && _messageTcpSenderThread.IsAlive)
			_messageTcpSenderThread.Join();

		if (Thread.CurrentThread != _messageUdpSenderThread && _messageUdpSenderThread.IsAlive) 
			_messageUdpSenderThread.Join();

		Cts.Dispose();
		
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
