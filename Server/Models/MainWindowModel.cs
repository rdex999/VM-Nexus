using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Shared;

namespace Server.Models;

public class MainWindowModel
{
	private Thread _listener;
	private CancellationTokenSource _listenerCts;

	public void ServerStart()
	{
		_listenerCts = new CancellationTokenSource();
		_listener = new Thread(() => Listen(_listenerCts.Token));
		_listener.Start();
	}

	public void ServerStop()
	{
		_listenerCts.Cancel();
		_listener.Join();
		_listenerCts.Dispose();
	}
	
	private void Listen(CancellationToken token)
	{
		IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());		/* Get local host ip addresses */

		/* Filter out ip addresses that are not IPv4, and loop-back ip's. Basically leave only usable ip's. Then from the array get the first ip, or null if empty. */
		IPAddress? ipAddr = ipHost.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
		if (ipAddr == null)
			return;
			
		IPEndPoint localEndPoint = new IPEndPoint(ipAddr, SharedDefinitions.ServerPort);			/* Combination of IP and port - End point. */
	
		Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);		/* Create the socket */
		socket.Bind(localEndPoint);																	/* Associate the IP address and port (end point) in the socket */
		socket.Listen(10);																	/* Listen for incoming connections */
		
		while (token.IsCancellationRequested == false)
		{
			Debug.WriteLine("Waiting for connection...");
			if (socket.Poll(10000, SelectMode.SelectRead))		/* Similar to Accept(), but blocks for a specified time. Returns true if there is a connection */
			{
				Socket client = socket.Accept();							/* There is a client in queue, accept him */
				
				Debug.WriteLine("Client connected.");
				Thread.Sleep(5000);
				
				client.Shutdown(SocketShutdown.Both);					/* Disable sending and receiving of data */
				client.Close();												/* Free used resources and close the socket */
			}
		}
		socket.Close();
	}
}