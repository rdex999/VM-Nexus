using System.Security.Cryptography;

namespace Shared.Networking;

public partial class MessagingService
{
	protected class UdpCryptoService : IDisposable
	{
		private ulong _sendCounter = 0;
		private ulong _receiveCounter = 0;
		private AesGcm _aesGcm;
		private byte[] _salt4;

		public UdpCryptoService(byte[] key32, byte[] salt4)
		{
			_aesGcm = new AesGcm(key32, 16);
			_salt4 = salt4;
		}

		public UdpCryptoService(out byte[] key32, out byte[] salt4)
		{
			key32 = RandomNumberGenerator.GetBytes(32);
			salt4 = RandomNumberGenerator.GetBytes(4);
			_salt4 = salt4;
			
			_aesGcm = new AesGcm(key32, 16);
		}

		public void Dispose()
		{
			_aesGcm.Dispose();
		}
	}
}