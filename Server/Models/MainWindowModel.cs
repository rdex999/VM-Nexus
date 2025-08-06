using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Shared;

namespace Server.Models;

public class MainWindowModel
{
	private Thread? _listener;
	private CancellationTokenSource? _listenerCts;
	private LinkedList<Client>? _clients;	
	
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
	
		_clients = new LinkedList<Client>();
		
		_listener = new Thread(() => ListenForClients(_listenerCts.Token, socket));
		_listener.Start();
		return ExitCode.Success;
	}

	public void ServerStop()
	{
		if (_listener != null && _listenerCts != null && _listener.IsAlive)
		{
			_listenerCts.Cancel();
			_listener.Join();
			_listenerCts.Dispose();
		}

		if (_clients == null)
			return;
		
		while (_clients.FirstOrDefault() != null)
		{
			Client client = _clients.First();
			client.Disconnect();
		}
	}
	
	private void ListenForClients(CancellationToken token, Socket socket)
	{
		socket.Listen();													/* Listen for incoming connections */
		
		while (token.IsCancellationRequested == false)
		{
			if (socket.Poll(10000, SelectMode.SelectRead))		/* Similar to Accept(), but blocks for a specified time. Returns true if there is a connection */
			{
				Socket clientSocket = socket.Accept();						/* There is a client in the queue, accept him */
			
				Client client = new Client(clientSocket);
				client.Disconnected += DisconnectedHandler;
				_clients!.AddLast(client);
			}
		}
		socket.Close();
	}

	private void DisconnectedHandler(object? sender, EventArgs args)
	{
		Client? client = (Client?)sender;
		if (client != null)
		{
			client.Disconnected -= DisconnectedHandler;
			_clients!.Remove(client);
		}
	}
}