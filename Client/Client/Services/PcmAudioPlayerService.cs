using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Client.Services;

public class PcmAudioPlayerService
{
	private ConcurrentQueue<byte[]> _packets;
	private CancellationTokenSource _cts = null!;
	private Thread _thread;

	public PcmAudioPlayerService()
	{
		_packets = new ConcurrentQueue<byte[]>();
		_thread = new Thread(AudioPlayer);
	}

	public void Initialize()
	{
		_cts = new CancellationTokenSource();
		_thread.Start();
	}

	public void Close()
	{
		throw new NotImplementedException();
	}

	public void EnqueuePacket(byte[] packet)
	{
		throw new NotImplementedException();
	}

	private void AudioPlayer()
	{
		while (!_cts.IsCancellationRequested)
		{
			Thread.Sleep(10);
		}
	}
}