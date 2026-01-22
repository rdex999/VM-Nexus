namespace Shared.Networking;

public partial class MessagingService
{
	public class DownloadHandler : TransferHandler
	{
		private Stream _destination;
		private TaskCompletionSource _tcs;

		public DownloadHandler(ulong size, string filePath)
			: base(size)
		{
			_destination = new FileStream(filePath, FileMode.Create);
			_tcs = new TaskCompletionSource();
			Task = _tcs.Task;
		}

		public DownloadHandler(ulong size, Stream destination)
			: base(size)
		{
			_destination = destination;
			_tcs = new TaskCompletionSource();
			Task = _tcs.Task;
		}

		/// <summary>
		/// Received the given data at the given offset. Writes data to file.
		/// </summary>
		/// <param name="data">The received data. data != null.</param>
		/// <param name="offset">The offset at which the received data belongs.</param>
		/// <remarks>
		/// Precondition: Download data was received. data != null. <br/>
		/// Postcondition: Data is received and written to the file.
		/// </remarks>
		public override async Task ReceiveAsync(byte[] data, ulong offset)
		{
			if (!IsDownloading)
				return;

			if (offset >= (ulong)_destination.Length)
			{
				try
				{
					_destination.SetLength((long)offset + 1);
				}
				catch (Exception)
				{
					IsDownloading = false;
					await _destination.DisposeAsync();
					_tcs.SetResult();
					RaiseFailed();
					return;
				}
			}

			_destination.Seek((long)offset, SeekOrigin.Begin);
			await _destination.WriteAsync(data);

			BytesReceived += (ulong)data.Length;
			if (BytesReceived == Size)
			{
				IsDownloading = false;
				await _destination.DisposeAsync();
				_tcs.SetResult();
				RaiseCompleted();
			}
			
			RaiseDataReceived();
		}
	}
}