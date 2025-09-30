using System.Collections.Concurrent;
using System.Threading;
using OpenTK.Audio.OpenAL;
using Shared;

namespace Client.Services;

public class PcmAudioPlayerService
{
	private ConcurrentQueue<byte[]> _packets;
	private CancellationTokenSource _cts = null!;
	private Thread _thread;
	private int _source = -1;
	private int[] _buffers = null!;

	public PcmAudioPlayerService()
	{
		_packets = new ConcurrentQueue<byte[]>();
		_thread = new Thread(AudioPlayer);
	}

	public ExitCode Initialize()
	{
		_cts = new CancellationTokenSource();
	
		ALDevice? device = ALC.OpenDevice(null);
		if (device == null) return ExitCode.OpenAlDeviceOpenFailed;

		ALContext? context = ALC.CreateContext(device.Value, new ALContextAttributes());
		if (context == null) return ExitCode.OpenAlInitializationFailed;

		ALC.MakeContextCurrent(context.Value);
	
		_source = AL.GenSource();
		_buffers = AL.GenBuffers(8);
		_thread.Start();
		
		return ExitCode.Success;
	}

	public void Close()
	{
		_cts.Cancel();
		_cts.Dispose();
		
		AL.SourceStop(_source);
		AL.DeleteSource(_source);
		AL.DeleteBuffers(_buffers);

		ALContext context = ALC.GetCurrentContext();
		ALDevice device = ALC.GetContextsDevice(context);
		ALC.DestroyContext(context);
		ALC.CloseDevice(device);
	}

	public void EnqueuePacket(byte[] packet)
	{
		_packets.Enqueue(packet);
	}

	private void AudioPlayer()
	{
		byte[] silence = new byte[5760];

		foreach (int buffer in _buffers)
		{
			AL.BufferData(buffer, ALFormat.Stereo16, silence, SharedDefinitions.AudioFramesFrequency);
			AL.SourceQueueBuffer(_source, buffer);
		}
		AL.SourcePlay(_source);

		while (!_cts.IsCancellationRequested)
		{
			AL.GetSource(_source, ALGetSourcei.BuffersProcessed, out int processed);
			while (processed-- > 0)
			{
				int buffer = AL.SourceUnqueueBuffer(_source);

				if(_packets.TryDequeue(out byte[]? packet)) 
				{
					AL.BufferData(buffer, ALFormat.Stereo16, packet, SharedDefinitions.AudioFramesFrequency);
				}
				else
				{
					AL.BufferData(buffer, ALFormat.Stereo16, silence, SharedDefinitions.AudioFramesFrequency);
				}
				AL.SourceQueueBuffer(_source, buffer);
			}

			AL.GetSource(_source, ALGetSourcei.SourceState, out int state);
			if ((ALSourceState)state != ALSourceState.Playing)
			{
				AL.SourcePlay(_source);
			}
			Thread.Sleep(5);
		}
	}
}