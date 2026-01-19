namespace Shared.Networking;

public partial class MessagingService
{
	private readonly struct UdpPacket
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
}