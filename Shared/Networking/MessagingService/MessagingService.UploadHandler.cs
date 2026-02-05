using System.Diagnostics;

namespace Shared.Networking;

public partial class MessagingService
{
	public class UploadHandler : TransferHandler
	{
		private readonly MessagingService _messagingService;
		private readonly Stream _source;

		public UploadHandler(MessagingService messagingService, Stream source)
			: base((ulong)source.Length)
		{
			_messagingService = messagingService;
			_source = source;
		}

		/// <summary>
		/// Received the given data at the given offset. Sends info message to the user side.
		/// </summary>
		/// <param name="data">The received data. data != null.</param>
		/// <param name="offset">The offset at which the received data belongs.</param>
		/// <remarks>
		/// Precondition: Upload data was received. data != null. <br/>
		/// Postcondition: Data is received and sent to the other side.
		/// </remarks>
		public override Task ReceiveAsync(byte[] data, ulong offset)
		{
			BytesReceived += (ulong)data.Length;
			_messagingService.SendInfo(new MessageInfoTransferData(Id, offset, data));
			RaiseDataReceived();
			return Task.CompletedTask;
		}

		/// <summary>
		/// Starts the transfer. Sets the transfer ID to the given ID. Starts to send data to the other side.
		/// </summary>
		/// <param name="id">The ID to identify this upload with. id != null &amp;&amp; id != Guid.Empty.</param>
		/// <remarks>
		/// Precondition: id != null &amp;&amp; id != Guid.Empty. <br/>
		/// Postcondition: Upload is started and is identified by the given ID. ReceiveAsync methods can now be used.
		/// </remarks>
		public override void Start(Guid id)
		{
			base.Start(id);
			Task = UploadAsync();
		}

		/// <summary>
		/// Runs the main uploading loop. Reads from the stream and sends data to the other side.
		/// </summary>
		/// <remarks>
		/// Precondition: The Start() method was called. <br/>
		/// Postcondition: While running, reads data from the stream and sends it to the other side. When completed, either the upload has completed or failed.
		/// In either case, the upload is ended.
		/// </remarks>
		private async Task UploadAsync()
		{
			byte[] buffer = new byte[Math.Min(16 * 1024, _source.Length - _source.Position)];
			double uploadBps = 1024 * 0.5;
			while (_source.Position < _source.Length && IsDownloading)
			{
				int readSize = (int)Math.Min(buffer.Length, _source.Length - _source.Position);

				Stopwatch readSw = Stopwatch.StartNew();
				await _source.ReadExactlyAsync(buffer, 0, readSize).ConfigureAwait(false);
				readSw.Stop();
			
				Stopwatch uploadSw = Stopwatch.StartNew();
				await _messagingService.TransferLimiter.AcquireAsync(readSize).ConfigureAwait(false);
				uploadSw.Stop();

				await ReceiveAsync(buffer[..readSize], (ulong)_source.Position - (ulong)readSize).ConfigureAwait(false);
				
				double readSeconds = readSw.Elapsed.TotalSeconds <= 0 ? double.MinValue : readSw.Elapsed.TotalSeconds;
				double uploadSeconds = uploadSw.Elapsed.TotalSeconds <= 0 ? double.MinValue : uploadSw.Elapsed.TotalSeconds;
				double readBps = readSize / readSeconds;
				if (readBps > uploadBps && uploadSeconds < 1)
					uploadBps *= 1 + 100 / Math.Sqrt(uploadBps);
				else
					uploadBps *= 1 / uploadSeconds;

				uploadBps = Math.Min(Math.Max(1, uploadBps), 1 * 1024 * 1024 * 1024);
				buffer = new byte[(long)Math.Ceiling(uploadBps)];
			}

			if (_source.Position == _source.Length)
				RaiseCompleted();
			else
				RaiseFailed();
			
			try
			{
				await _source.DisposeAsync().ConfigureAwait(false);
			}
			catch (ObjectDisposedException)
			{ }
		}
	}
}