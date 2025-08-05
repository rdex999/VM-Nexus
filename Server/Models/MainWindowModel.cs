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

	public ExitCode ServerStart()
	{
		_listenerCts = new CancellationTokenSource();
		
		IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());		/* Get local host ip addresses */

		/* Filter out ip addresses that are not IPv4, and loop-back ip's. Basically leave only usable ip's. Then from the array get the first ip, or null if empty. */
		IPAddress? ipAddr = ipHost.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
		if (ipAddr == null)
			return ExitCode.ServerNoValidLocalhostIp;
				
		IPEndPoint localEndPoint = new IPEndPoint(ipAddr, SharedDefinitions.ServerPort);			/* Combination of IP and port - End point. */
	
		Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);		/* Create the socket */	
		socket.Bind(localEndPoint);																	/* Associate the IP address and port (end point) in the socket */
		
		_listener = new Thread(() => ListenForClients(_listenerCts.Token, socket));
		_listener.Start();
		return ExitCode.Success;
	}

	public void ServerStop()
	{
		_listenerCts.Cancel();
		_listener.Join();
		_listenerCts.Dispose();
	}
	
	private void ListenForClients(CancellationToken token, Socket socket)
	{
		socket.Listen();													/* Listen for incoming connections */
		
		while (token.IsCancellationRequested == false)
		{
			Debug.WriteLine("Waiting for connection...");
			if (socket.Poll(10000, SelectMode.SelectRead))		/* Similar to Accept(), but blocks for a specified time. Returns true if there is a connection */
			{
				Socket client = socket.Accept();							/* There is a client in the queue, accept him */
				
				Debug.WriteLine("Client connected.");
				Thread.Sleep(5000);
				
				client.Shutdown(SocketShutdown.Both);					/* Disable sending and receiving of data */
				client.Close();												/* Free used resources and close the socket */
			}
		}
		socket.Close();
	}
}