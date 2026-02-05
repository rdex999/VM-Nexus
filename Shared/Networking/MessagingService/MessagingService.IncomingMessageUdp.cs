using System.Collections;
using Timer = System.Timers.Timer;

namespace Shared.Networking;

public partial class MessagingService
{
	private class IncomingMessageUdp
	{
		private readonly Action<Guid>? _timeout;
		public Guid MessageId { get; }
		private readonly Timer _timeoutTimer;
		private readonly byte[] _data;
		private int _bytesReceived = 0;
		private readonly BitArray _chunks;

		/// <summary>
		/// Creates this incoming message and loads the first packet using ReceivePacket().
		/// </summary>
		/// <param name="firstPacket">The first packet that was received. firstPacket != null.</param>
		/// <param name="timeout">A callback for when this message has timed-out. When raised, this message should be removed.</param>
		/// <param name="result">An exit code indicating the result of receiving the first packet. (See ReceivePacket() documentation.)</param>
		/// <param name="message">If this was the last packet, the output message is written to this pointer. Null is written otherwise.</param>
		/// <remarks>
		/// Precondition: A packet was received and no instance of IncomingMessageUdp exists for it. firstPacket != null. <br/>
		/// Postcondition: This IncomingMessageUdp is created. As for the result and message parameters, see documentation of ReceivePacket().
		/// </remarks>
		public IncomingMessageUdp(UdpPacket firstPacket, Action<Guid> timeout, out ExitCode result, out IMessage? message)
		{
			_timeout += timeout;
			_timeoutTimer = new Timer(TimeSpan.FromSeconds(3));
			_timeoutTimer.Elapsed += (_, _) =>
			{
				_timeout?.Invoke(MessageId);
				_timeoutTimer.Dispose();
			};
			_timeoutTimer.Start();
			
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
		public ExitCode ReceivePacket(UdpPacket packet, out IMessage? message)
		{
			message = null;

			if (packet.MessageSize != _data.Length)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}
			
			int chunk = packet.Offset / UdpPacket.MaxPayloadSize;
			if (chunk >= _chunks.Length)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}

			if (_chunks[chunk])
				return ExitCode.UdpPacketDuplicate;

			int expectedChunkSize = Math.Min(UdpPacket.MaxPayloadSize, _data.Length - packet.Offset);
			if (packet.PayloadSize != expectedChunkSize)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}
			
			packet.Payload.CopyTo(_data.AsSpan(packet.Offset));
			_chunks[chunk] = true;
			_bytesReceived += expectedChunkSize;

			if (_bytesReceived >= packet.MessageSize)
			{
				Close();
				
				IMessage? msg = (IMessage?)Common.FromByteArrayWithType(_data);
				if (msg == null)
					return ExitCode.MessageUdpCorrupted;

				message = msg;
				return ExitCode.Success;
			}

			return ExitCode.MessageUdpNotCompleted;
		}

		/// <summary>
		/// Check if the packet can be received. If It's invalid, this message is closed. <br/>
		/// Note: If this method returns ExitCode.Success, it is not guaranteed that ReceivePacket will succeed.
		/// </summary>
		/// <param name="packet">The packet to check if it can be received. packet != null.</param>
		/// <returns>An exit code, indicating the packet can be received, (success) or the reason it cannot be received.</returns>
		/// <remarks>
		/// Precondition: packet != null. <br/>
		/// Postcondition: If the packet can be received, success is returned.
		/// Otherwise, the returned exit code indicates the reason the packet cannot be received.
		/// If ExitCode.InvalidUdpPacket is returned, this incoming message is closed,
		/// and the caller should remove this message from the incoming messages.
		/// </remarks>
		public ExitCode CanReceivePacket(UdpPacket packet)
		{
			if (packet.MessageSize != _data.Length)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}
			
			int chunk = packet.Offset / UdpPacket.MaxPayloadSize;
			if (chunk >= _chunks.Length)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}

			if (_chunks[chunk])
				return ExitCode.UdpPacketDuplicate;

			int expectedChunkSize = Math.Min(UdpPacket.MaxPayloadSize, _data.Length - packet.Offset);
			if (packet.PayloadSize != expectedChunkSize)
			{
				Close();
				return ExitCode.InvalidUdpPacket;
			}
			
			return ExitCode.Success;
		}

		/// <summary>
		/// Closes this message and releases used resources.
		/// </summary>
		/// <remarks>
		/// Precondition: The processing of this message has finished. (got corrupted, finished, timeout, etc.) <br/>
		/// Postcondition: This message is closed, used resources are freed.
		/// </remarks>
		private void Close()
		{
			_timeoutTimer.Stop();
			_timeoutTimer.Dispose();
		}
	}
}