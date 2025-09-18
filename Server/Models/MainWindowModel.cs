using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Server.Services;
using Shared;

namespace Server.Models;

public class MainWindowModel
{
	private Thread? _listener;
	private CancellationTokenSource? _listenerCts;
	private LinkedList<ClientConnection> _clients;	
	private DatabaseService _databaseService;
	private VirtualMachineService _virtualMachineService;
	private DriveService _driveService;

	public MainWindowModel()
	{
		_clients = new LinkedList<ClientConnection>();
		_databaseService = new DatabaseService();
		_driveService = new DriveService(_databaseService);
		_virtualMachineService = new VirtualMachineService(_databaseService, _driveService);
	}
	
	/// <summary>
	/// Starts the server.
	/// </summary>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: Server is not running. <br/>
	/// Postcondition: On success, the returned exit code will indicate success, and the server will be running and listening for clients. <br/>
	/// On failure, the returned exit code will indicate the error, and the server will not be running.
	/// </remarks>
	public async Task<ExitCode> ServerStartAsync()
	{
		_listenerCts = new CancellationTokenSource();

		ExitCode status = await _databaseService.InitializeAsync();
		if(status != ExitCode.Success)
		{
			return status;
		}
		
		/* Socket initialization and listening */
		IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());		/* Get local host ip addresses */

		/* Filter out ip addresses that are not IPv4, and loop-back ip's. Basically leave only usable ip's. Then from the array get the first ip, or null if empty. */
		IPAddress? ipAddr = ipHost.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));
		if (ipAddr == null)
			return ExitCode.ServerNoValidLocalhostIp;
				
		IPEndPoint localEndPoint = new IPEndPoint(ipAddr, SharedDefinitions.ServerPort);			/* Combination of IP and port - End point. */
	
		Socket socket = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);		/* Create the socket */	
		socket.Bind(localEndPoint);																	/* Associate the IP address and port (end point) in the socket */
	
		_clients = new LinkedList<ClientConnection>();
		
		_listener = new Thread(() => ListenForClients(_listenerCts.Token, socket));
		_listener.Start();

		return ExitCode.Success;
	}

	/// <summary>
	/// Stops the server.
	/// </summary>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// </returns>
	/// <remarks>
	/// Precondition: The server is up and running. <br/>
	/// Postcondition: On success, the returned exit code will indicate success, and the server will be shut down. <br/>
	/// On failure, the returned exit code will indicate the error, and the server will keep running.
	/// </remarks>
	public ExitCode ServerStop()
	{
		if (_listenerCts != null)
		{
			_listenerCts.Cancel();
			_listenerCts.Dispose();
		}
		
		if (_listener != null && _listener.IsAlive)
		{
			_listener.Join();
		}

		while (_clients.FirstOrDefault() != null)
		{
			ClientConnection clientConnection = _clients.First();
			clientConnection.Disconnect();
		}

		_databaseService.Close();
		_virtualMachineService.Close();

		return ExitCode.Success;
	}

	/// <summary>
	/// Listens for client connections and redirects them to handlers.
	/// </summary>
	/// <param name="token">
	/// Used to determine when to stop listening for clients. token != null.
	/// </param>
	/// <param name="socket">
	/// The socket to listen for clients on. socket != null.
	/// </param>
	/// <remarks>
	/// Precondition: token != null &amp;&amp; socket != null. <br/>
	/// Postcondition: socket is closed, server does not listen for clients anymore.
	/// </remarks>
	private void ListenForClients(CancellationToken token, Socket socket)
	{
		socket.Listen();													/* Listen for incoming connections */
		
		while (!token.IsCancellationRequested)
		{
			if (socket.Poll(10000, SelectMode.SelectRead))		/* Similar to Accept(), but blocks for a specified time. Returns true if there is a connection */
			{
				Socket clientSocket = socket.Accept();						/* There is a client in the queue, accept him */
			
				ClientConnection clientConnection = new ClientConnection(clientSocket, _databaseService, _virtualMachineService, _driveService);
				clientConnection.Disconnected += DisconnectedHandler;
				_clients.AddLast(clientConnection);
			}
		}
		socket.Close();
	}

	/// <summary>
	/// Handles a client disconnection. Called by the Disconnected event in ClientConnection.
	/// </summary>
	/// <param name="sender">
	/// The client connection that was disconnected.
	/// </param>
	/// <param name="args">
	/// Always empty (EventArgs.Empty)
	/// </param>
	/// <remarks>
	/// Precondition: Client has disconnected. sender is the ClientConnection that was disconnected. sender != null. args is not used. <br/>
	/// Postcondition: Client is removed from the client connection list.
	/// </remarks>
	private void DisconnectedHandler(object? sender, EventArgs args)
	{
		ClientConnection? client = (ClientConnection?)sender;
		if (client != null)
		{
			client.Disconnected -= DisconnectedHandler;
			_clients.Remove(client);
		}
	}
}