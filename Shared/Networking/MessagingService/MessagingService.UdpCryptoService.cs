using System.Security.Cryptography;

namespace Shared.Networking;

public partial class MessagingService
{
	private class UdpCryptoService : IDisposable
	{
		private ulong _sendCounter = 0;
		private AesGcm _aesGcm;
		private byte[] _salt4;
		private readonly Lock _syncLock = new();

		public UdpCryptoService(byte[] key32, byte[] salt4) => Reset(key32, salt4);
		public UdpCryptoService(out byte[] key32, out byte[] salt4) => Reset(out key32, out salt4);

		/// <summary>
		/// Reset this service. Re-generate key and salt, reset counters.
		/// </summary>
		/// <param name="key32">The new key to use. key32 != null.</param>
		/// <param name="salt4">The new salt to use. salt != null.</param>
		/// <remarks>
		/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
		/// Postcondition: Service is reset, the given key and salt are now used.
		/// </remarks>
		public void Reset(byte[] key32, byte[] salt4)
		{
			AesGcm aesGcm = new AesGcm(key32, 16);
			lock (_syncLock)
			{
				AesGcm? old = Interlocked.Exchange(ref _aesGcm, aesGcm);
				_salt4 = salt4;
				Interlocked.Exchange(ref _sendCounter, 0);
				old?.Dispose();
			}
		}
	
		/// <summary>
		/// Reset this service. Re-generate key and salt, reset counters.
		/// </summary>
		/// <param name="key32">The new generated key output. key32 != null.</param>
		/// <param name="salt4">The new generated salt output. salt != null.</param>
		/// <remarks>
		/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
		/// Postcondition: Service is reset, the new key and salt are written into the given outputs.
		/// </remarks>
		public void Reset(out byte[] key32, out byte[] salt4)
		{
			key32 = RandomNumberGenerator.GetBytes(32);
			salt4 = RandomNumberGenerator.GetBytes(4);
			Reset(key32, salt4);
		}

		/// <summary>
		/// Encrypts the given payload in builds a UDP packet, ready to be sent.
		/// </summary>
		/// <param name="messageId">The ID of the message that this packet is a part of. messageId != null.</param>
		/// <param name="messageSize">The size of the entier message, in bytes. messageSize > 0.</param>
		/// <param name="offset">The offset of this packet's payload in the message. offset >= 0.</param>
		/// <param name="payload">The payload, the content of this packet.
		/// payload != null &amp;&amp; payload.Length > 0 &amp;&amp; payload.Lenght &lt;= DatagramSize - HeaderSize.</param>
		/// <returns>A UDP packet of which data is encrypted, ready to be sent. Returns null on failure.</returns>
		/// <remarks>
		/// Precondition: messageId != null &amp;&amp; messageId != Guid.Empty &amp;&amp; messageSize > 0 &amp;&amp;
		/// offset >= 0 &amp;&amp; payload != null &amp;&amp; payload.Length > UdpPacket.HeaderSize &amp;&amp;
		/// payload.Length &lt;= UdpPacket.MaxPayloadSize. <br/>
		/// Postcondition: On success, a UDP packet of which data is encrypted is returned, ready to be sent.
		/// On failure, null is returned.
		/// </remarks>
		public UdpPacket? Encrypt(Guid messageId, int messageSize, int offset, ReadOnlySpan<byte> payload)
		{
			ulong sequence = _sendCounter;
			Interlocked.Increment(ref _sendCounter);
			
			byte[] nonce = BuildNonce(sequence);
			byte[] tag = new byte[16];
			byte[] cipher = new byte[payload.Length];

			try
			{
				_aesGcm.Encrypt(nonce, payload, cipher, tag, null);
			}
			catch (Exception)
			{
				return null;
			}

			return new UdpPacket(sequence, tag, messageId, messageSize, offset, cipher);
		}

		/// <summary>
		/// Decrypts the given packet. Returns a new packet with the decrypted payload.
		/// </summary>
		/// <param name="packet">The packet to decrypt. packet != null.</param>
		/// <returns>The decrypted packet, or null on failure.</returns>
		/// <remarks>
		/// Precondition: The given packet is valid. packet != null. <br/>
		/// Postcondition: On success, the decrypted packet is returned. On failure, null is returned.
		/// </remarks>
		public UdpPacket? Decrypt(UdpPacket packet)
		{
			byte[] nonce = BuildNonce(packet.Sequence);
			byte[] plainText = new byte[packet.Payload.Length];

			try
			{
				_aesGcm.Decrypt(nonce, packet.Payload, packet.Tag16, plainText, null);
			}
			catch (Exception)
			{
				return null;
			}

			if (!packet.UpdatePayload(plainText))
				return null;
			
			return packet;
		}

		/// <summary>
		/// Builds a nonce using the given sequence number.
		/// </summary>
		/// <param name="sequence">The sequence number to use for the nonce.</param>
		/// <returns>The built 12 byte nonce.</returns>
		/// <remarks>
		/// Precondition: No specific precondition. <br/>
		/// Postcondition: The built 12 byte nonce is returned.
		/// </remarks>
		private byte[] BuildNonce(ulong sequence)
		{
			byte[] nonce = new byte[12];
			_salt4.CopyTo(nonce, 0);
			BitConverter.GetBytes(sequence).CopyTo(nonce, 4);
			
			return nonce;
		}
	
		/// <summary>
		/// Disposes this service, frees unmanaged resources.
		/// </summary>
		/// <remarks>
		/// Precondition: No specific precondition. <br/>
		/// Postcondition: This service is disposed, resources are freed.
		/// </remarks>
		public void Dispose()
		{
			_aesGcm.Dispose();
		}
	}
}