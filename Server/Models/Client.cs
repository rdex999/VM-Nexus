using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Shared.Networking;

namespace Server.Models;

public sealed class Client : MessagingService
{
	public event EventHandler Disconnected;

	public Client(Socket socket)
		: base(socket)
	{
		InitializeAsync().Wait();	/* Doesnt contain long-running code, so its fine to just Wait() it here */
	}

	/* When the client suddenly disconnects, delete this client object, and let it be re-created in ListenForClients */
	protected override void HandleSuddenDisconnection()
	{
		base.HandleSuddenDisconnection();
		AfterDisconnection();
	}

	protected override void AfterDisconnection()
	{
		Disconnected?.Invoke(this, EventArgs.Empty);
	}
}