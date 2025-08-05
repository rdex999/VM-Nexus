using System;
using System.Net.Sockets;
using System.Threading;

namespace Server.Models;

public class Client
{
	private Socket _socket;
	private Thread _thread;
	private CancellationTokenSource _cts;

	public event EventHandler Disconnected;
	
	public Client(Socket socket)
	{
		_socket = socket;
		_cts = new CancellationTokenSource();
		_thread = new Thread(() => Communicate(_cts.Token));
		_thread.Start();
	}
	
	private void Communicate(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			Thread.Sleep(5000);
			_cts.Cancel();	/* For testing */
		}
		Disconnect();
	}
	
	public void Disconnect()
	{
		_cts.Cancel();

		if (Thread.CurrentThread != _thread)
		{
			_thread.Join();
		}
		
		_socket.Dispose();
		_socket.Close();
	
		Disconnected?.Invoke(this, EventArgs.Empty);
	}
}