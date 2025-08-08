using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shared;

namespace Server.Models;

public class MainWindowModel
{
	private Thread? _listener;
	private CancellationTokenSource? _listenerCts;
	private LinkedList<ClientConnection>? _clients;	
	private NpgsqlConnection _connection;
	
	public async Task<ExitCode> ServerStartAsync()
	{
		_listenerCts = new CancellationTokenSource();

		/* PostgreSQL startup */
		_connection = new NpgsqlConnection(connectionString: "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=VM_Nexus_DB;");
		_connection.Open();
		
		NpgsqlCommand command = _connection.CreateCommand();
		
		#if DEBUG
			command.CommandText = "DROP TABLE IF EXISTS users;";
			await command.ExecuteNonQueryAsync();
		#endif
		
		/* TODO: Generate salts and fill in the length of a salt here */
		command.CommandText = $"""
		                       CREATE TABLE IF NOT EXISTS users (
		                           		username VARCHAR({SharedDefinitions.CredentialsMaxLength}), 
		                           		password_hashed VARCHAR(255), 
		                           		password_salt VARCHAR(255)
		                           	)
		                       """;
		
		await command.ExecuteNonQueryAsync();
		
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

	public async Task<ExitCode> ServerStopAsync()
	{
		if (_listener != null && _listenerCts != null && _listener.IsAlive)
		{
			_listenerCts.Cancel();
			_listener.Join();
			_listenerCts.Dispose();
		}

		if (_clients != null)
		{
			List<Task> tasks = new List<Task>();
			while (_clients.FirstOrDefault() != null)
			{
				ClientConnection clientConnection = _clients.First();
				tasks.Add(clientConnection.DisconnectClient());
			}

			await Task.WhenAll(tasks);
		}

		return ExitCode.Success;
	}
	
	private void ListenForClients(CancellationToken token, Socket socket)
	{
		socket.Listen();													/* Listen for incoming connections */
		
		while (token.IsCancellationRequested == false)
		{
			if (socket.Poll(10000, SelectMode.SelectRead))		/* Similar to Accept(), but blocks for a specified time. Returns true if there is a connection */
			{
				Socket clientSocket = socket.Accept();						/* There is a client in the queue, accept him */
			
				ClientConnection clientConnection = new ClientConnection(clientSocket);
				clientConnection.Disconnected += DisconnectedHandler;
				_clients!.AddLast(clientConnection);
			}
		}
		socket.Close();
	}

	private void DisconnectedHandler(object? sender, EventArgs args)
	{
		ClientConnection? client = (ClientConnection?)sender;
		if (client != null)
		{
			client.Disconnected -= DisconnectedHandler;
			_clients!.Remove(client);
		}
	}
}