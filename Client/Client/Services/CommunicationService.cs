using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Shared;

namespace Client.Services;

/* Responsible for communicating with the server */
public class CommunicationService
{
	private Socket _socket;
	private Thread _thread;								/* Runs the Communicate function */
	private CancellationTokenSource _cts;
	public event EventHandler<ExitCode> FailEvent;

	public CommunicationService()
	{
		_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		
		_cts = new CancellationTokenSource();
		_thread = new Thread(() => Communicate(_cts.Token));

		try
		{
			_socket.Connect(IPAddress.Parse(SharedDefinitions.ServerIp), SharedDefinitions.ServerPort);
		}
		catch (Exception e)
		{
			/* TODO: Handle failure */
		}
		
		_thread.Start();
	}

	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			
		}
	}
}