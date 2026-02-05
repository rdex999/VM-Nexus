namespace Shared.Networking;

public partial class MessagingService
{
	private class UdpPacket
	{
		public const int HeaderSize = 4 + 8 + 16 + 16 + 4 + 4;
		public const int MaxPayloadSize = DatagramSize - HeaderSize;
		public ulong Sequence { get; }
		public ReadOnlySpan<byte> Tag16 => _packet.AsSpan(4 + 8, 16);
		public Guid MessageId { get; }
		public int MessageSize { get; }
		public int PayloadSize { get; }
		public int Offset { get; }
		public ReadOnlySpan<byte> Payload => _packet.AsSpan(HeaderSize, PayloadSize);
		public ReadOnlySpan<byte> Packet => _packet.AsSpan();
		private readonly byte[] _packet;
	
		/*
		 * UDP Packet structure, by byte offset, and length. (offset:length)
		 * 
		 * 0:4		- Magic "VMNX"
		 * 4:8		- Sequence number. (ulong) Used for encryption and decryption. (See UdpCryptoService)
		 * 12:16	- Tag. Used for encryption and decryption. (See UdpCryptoService)
		 * 28:16	- Message ID (Guid)
		 * 44:4		- Message size. The total size of the incoming message, in bytes. (not the size of this packet)
		 * 48:4		- Offset. The offset of this packet's data in the incoming message, in bytes.
		 */
		
		public UdpPacket(byte[] packet, int packetSize)
		{
			PayloadSize = packetSize - HeaderSize;
			_packet = packet;
			
			int nextField = 0;
			nextField += MessageMagic.Length;
			
			Sequence = BitConverter.ToUInt64(_packet, nextField);
			nextField += sizeof(ulong) + Tag16.Length;
			
			MessageId = new Guid(packet.AsSpan(nextField, 16));
			nextField += 16;
		
			MessageSize = BitConverter.ToInt32(_packet.AsSpan(nextField, sizeof(int)));
			nextField += sizeof(int);
			
			Offset = BitConverter.ToInt32(_packet.AsSpan(nextField, sizeof(int)));
		}

		/// <summary>
		/// Instantiates a packet from the given data. (Packet content)
		/// </summary>
		/// <param name="sequence">The sequence number of the packet. Used for encryption and decryption.</param>
		/// <param name="tag16">The packets tag. Used for encryption and decryption. tag16 != null.</param>
		/// <param name="messageId">The ID of the message that this packet is a part of. messageId != null.</param>
		/// <param name="messageSize">The size of the entier message, in bytes. messageSize > 0.</param>
		/// <param name="offset">The offset of this packet's payload in the message. offset >= 0.</param>
		/// <param name="payload">The payload, the content of this packet.
		/// payload != null &amp;&amp; payload.Length > 0 &amp;&amp; payload.Lenght &lt;= DatagramSize - HeaderSize.</param>
		/// <returns>A byte array representing the packet with the given data.</returns>
		/// <remarks>
		/// Precondition: tag16 != null &amp;&amp; messageId != null &amp;&amp; messageSize > 0 &amp;&amp; offset >= 0
		/// &amp;&amp; payload != null &amp;&amp; payload.Length > 0 &amp;&amp; payload.Lenght &lt;= DatagramSize - HeaderSize. <br/>
		/// Postcondition: A byte array representing the packet with the given data is returned.
		/// </remarks>
		public UdpPacket(ulong sequence, ReadOnlySpan<byte> tag16, Guid messageId, int messageSize, int offset, ReadOnlySpan<byte> payload)
		{
			_packet = new byte[HeaderSize + payload.Length];
			int nextField = 0;
		
			MessageMagic.CopyTo(_packet, nextField);
			nextField += MessageMagic.Length;
			
			BitConverter.GetBytes(sequence).CopyTo(_packet, nextField);
			nextField += sizeof(ulong);
			
			tag16.CopyTo(_packet.AsSpan(nextField, 16));
			nextField += 16;
			
			messageId.ToByteArray().CopyTo(_packet, nextField);
			nextField += 16;
			
			BitConverter.GetBytes(messageSize).CopyTo(_packet, nextField);
			nextField += sizeof(int);
			
			BitConverter.GetBytes(offset).CopyTo(_packet, nextField);
			nextField += sizeof(int);
			
			payload.CopyTo(_packet.AsSpan(nextField));
		}

		/// <summary>
		/// Checks whether the given UDP packet is valid or not. (Checks magic and payload size)
		/// </summary>
		/// <param name="packet">The UDP packet. packet != null.</param>
		/// <param name="packetSize">The actual size of the packet. packetSize &lt;= packet.Length.</param>
		/// <returns>True if the UDP packet is valid, false otherwise.</returns>
		/// <remarks>
		/// This method should be called before constructing a UdpPacket instance. <br/>
		/// Precondition: A UDP packet was received, and validating it is required. packet != null. packetSize > 0 &amp;&amp; packetSize &lt;= packet.Length. <br/>
		/// Postcondition: Returns true if the UDP packet is valid, false otherwise.
		/// </remarks>
		public static bool IsValidPacket(byte[] packet, int packetSize)
		{
			if (packetSize <= 0 || packetSize > packet.Length)
				return false;
			
			if (packetSize > DatagramSize || packetSize < HeaderSize)
				return false;

			/* Protects against large memory allocation attacks. (attacker set large size so server allocates memory) */
			int messageSize = BitConverter.ToInt32(packet.AsSpan(44, 4));
			if (messageSize > SharedDefinitions.MaxUdpMessageSize || messageSize <= 0)
				return false;
			
			bool validMagic = packet.AsSpan(0, MessageMagic.Length).SequenceEqual(MessageMagic);

			return validMagic;
		}

		/// <summary>
		/// Update the payload to a new payload, of the same size. (Useful after decryption)
		/// </summary>
		/// <param name="payload">The new payload. Must be of the same size as the old one. payload != null.</param>
		/// <returns>True on success, false otherwise.</returns>
		/// <remarks>
		/// Precondition: The given payload must be of the same size as the old one. payload != null. <br/>
		/// Postcondition: If the given payload is of the same size as the old one, the operation succeeds and true is returned.
		/// Otherwise, false is returned and the payload is not updated.
		/// </remarks>
		public bool UpdatePayload(ReadOnlySpan<byte> payload)
		{
			if (payload.Length != PayloadSize)
				return false;
			
			payload.CopyTo(_packet.AsSpan(HeaderSize, Payload.Length));
			return true;
		}
	}
}