using System.Diagnostics;
using System.Security.Cryptography;

namespace Shared.Networking;

public partial class MessagingService
{
	private class UdpCryptoService : IDisposable
	{
		public event EventHandler? ResetRequired;
		private ulong _encCounter = 0;
		private AesGcm _aesGcmS2C;
		private AesGcm _aesGcmC2S;
		private byte[] _sessionId4;
		private readonly bool _isServer;
		private readonly Lock _syncLock = new();

		public UdpCryptoService(bool isServer, byte[] masterKey32, byte[] salt32)
		{
			_isServer = isServer;
			Reset(masterKey32, salt32);
		}

		public UdpCryptoService(bool isServer, out byte[] masterKey32, out byte[] salt32)
		{
			_isServer = isServer;
			Reset(out masterKey32, out salt32);
		}

		/// <summary>
		/// Reset this service. Re-generate key and salt, reset counters.
		/// </summary>
		/// <param name="masterKey32">The new key to use. key32 != null.</param>
		/// <param name="salt32">The new salt to use. salt != null.</param>
		/// <remarks>
		/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
		/// Postcondition: Service is reset, the given key and salt are now used.
		/// </remarks>
		public void Reset(byte[] masterKey32, byte[] salt32)
		{
			byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, masterKey32, salt32);

			byte[] s2C = HKDF.Expand(HashAlgorithmName.SHA256, prk, 32, "SERVER TO CLIENT"u8.ToArray());
			byte[] c2S = HKDF.Expand(HashAlgorithmName.SHA256, prk, 32, "CLIENT TO SERVER"u8.ToArray());
			byte[] sessionId4 = HKDF.Expand(HashAlgorithmName.SHA256, prk, 4, "SESSION ID"u8.ToArray());

			lock (_syncLock)
			{
				_sessionId4 = sessionId4;
				Interlocked.Exchange(ref _encCounter, 0);

				AesGcm aesGcmS2C = new AesGcm(s2C, 16);
				AesGcm aesGcmC2S = new AesGcm(c2S, 16);
				
				AesGcm? oldS2C = Interlocked.Exchange(ref _aesGcmS2C, aesGcmS2C);
				AesGcm? oldC2S = Interlocked.Exchange(ref _aesGcmC2S, aesGcmC2S);
				
				oldS2C?.Dispose();
				oldC2S?.Dispose();
			}
		}
	
		/// <summary>
		/// Reset this service. Re-generate key and salt, reset counters.
		/// </summary>
		/// <param name="masterKey32">The new generated key output. key32 != null.</param>
		/// <param name="salt32">The new generated salt output. salt != null.</param>
		/// <remarks>
		/// Precondition: key32 != null &amp;&amp; salt4 != null. <br/>
		/// Postcondition: Service is reset, the new key and salt are written into the given outputs.
		/// </remarks>
		public void Reset(out byte[] masterKey32, out byte[] salt32)
		{
			masterKey32 = RandomNumberGenerator.GetBytes(32);
			salt32 = RandomNumberGenerator.GetBytes(32);
			Reset(masterKey32, salt32);
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
			ulong sequence = Interlocked.Increment(ref _encCounter);
			
			byte[] nonce = BuildNonce(sequence);
			byte[] tag = new byte[16];
			byte[] cipher = new byte[payload.Length];

			try
			{
				if (_isServer)
					_aesGcmS2C.Encrypt(nonce, payload, cipher, tag, null);
				else
					_aesGcmC2S.Encrypt(nonce, payload, cipher, tag, null);
			}
			catch (Exception)
			{
				return null;
			}
			
			if (_encCounter > 1 << 24)
				ResetRequired?.Invoke(this, EventArgs.Empty);

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
				if (_isServer)
					_aesGcmC2S.Decrypt(nonce, packet.Payload, packet.Tag16, plainText, null);
				else
					_aesGcmS2C.Decrypt(nonce, packet.Payload, packet.Tag16, plainText, null);
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
			_sessionId4.CopyTo(nonce, 0);
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
			_aesGcmS2C?.Dispose();
			_aesGcmC2S?.Dispose();
		}
	}
}