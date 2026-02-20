namespace Shared.Networking;

public partial class MessagingService
{
	public abstract class TransferHandler
	{
		public event EventHandler? Completed;
		public event EventHandler? Failed;
		public event EventHandler? Ended;							/* Invoked on both Completed and Failed. */
		public event EventHandler? DataReceived; 
		public Guid Id { get; private set; } = Guid.Empty;
		public Task Task { get; protected set; }					/* Usable only after Start() has been called - Its in completed state before Start() */
		public bool IsStarted => Id != Guid.Empty;
		public bool IsDownloading { get; protected set; } = false;
		public ulong Size { get; }
		public ulong BytesReceived { get; protected set; } = 0;

		protected TransferHandler(ulong size)
		{
			Size = size;
			Task = Task.CompletedTask;
		}

		/// <summary>
		/// Starts the transfer. Sets the transfer ID to the given ID. <br/>
		/// Implement this method in child classes to set the Task property.
		/// </summary>
		/// <param name="id">The ID to identify this transfer with. id != null &amp;&amp; id != Guid.Empty.</param>
		/// <remarks>
		/// Precondition: id != null &amp;&amp; id != Guid.Empty. <br/>
		/// Postcondition: Transfer is started and is identified by the given ID. The Task property of this class instance can now be used.
		/// ReceiveAsync methods can now be used.
		/// </remarks>
		public virtual void Start(Guid id)
		{
			Id = id;
			IsDownloading = true;
		}

		/// <summary>
		/// Receives the received data. Receiving implementation varies by handler type.
		/// </summary>
		/// <param name="data">The received data. data != null.</param>
		/// <remarks>
		/// Precondition: Download data was received. data != null. <br/>
		/// Postcondition: Data is received.
		/// </remarks>
		public async Task ReceiveAsync(MessageInfoTransferData data) => await ReceiveAsync(data.Data, data.Offset);

		/// <summary>
		/// Received the given data at the given offset. Receiving implementation varies by handler type.
		/// </summary>
		/// <param name="data">The received data. data != null.</param>
		/// <param name="offset">The offset at which the received data belongs.</param>
		/// <remarks>
		/// Precondition: Download data was received. data != null. <br/>
		/// Postcondition: Data is received.
		/// </remarks>
		public abstract Task ReceiveAsync(byte[] data, ulong offset);
		
		/// <summary>
		/// Raises the Completed and Ended events.
		/// </summary>
		/// <remarks>
		/// Precondition: This transfer has completed. <br/>
		/// Postcondition: The Completed and Ended events are raised.
		/// </remarks>
		protected void RaiseCompleted()
		{
			Completed?.Invoke(this, EventArgs.Empty);
			Ended?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Raises the Failed and Ended events.
		/// </summary>
		/// <remarks>
		/// Precondition: This transfer has failed. <br/>
		/// Postcondition: The Failed and Ended events are raised.
		/// </remarks>
		protected void RaiseFailed()
		{
			Failed?.Invoke(this, EventArgs.Empty);
			Ended?.Invoke(this, EventArgs.Empty);
		}
		
		/// <summary>
		/// Raises the Data Received event.
		/// </summary>
		/// <remarks>
		/// Precondition: This transfer has failed. <br/>
		/// Postcondition: The Data Received event is raised.
		/// </remarks>	
		protected void RaiseDataReceived() => DataReceived?.Invoke(this, EventArgs.Empty);
	}
}