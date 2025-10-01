using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenTK.Audio.OpenAL;
using Shared;

namespace Client.Services;

public class PcmAudioPlayerService
{
	public bool IsInitialized { get; private set; }
	private ConcurrentQueue<byte[]> _packets;
	private CancellationTokenSource _cts = null!;
	private Thread _thread = null!;
	private int _source = -1;
	private int[] _buffers = null!;

	public PcmAudioPlayerService()
	{
		_packets = new ConcurrentQueue<byte[]>();
	}

	public ExitCode Initialize()
	{
		_cts = new CancellationTokenSource();
		_thread = new Thread(AudioPlayer);
	
		ALDevice? device = ALC.OpenDevice(null);		/* Can return null according to documentation */
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (device == null) return ExitCode.OpenAlDeviceOpenFailed;

		ALContext? context = ALC.CreateContext(device.Value, new ALContextAttributes());	/* Can return null according to documentation */
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (context == null) return ExitCode.OpenAlInitializationFailed;

		ALC.MakeContextCurrent(context.Value);
	
		_source = AL.GenSource();
		_buffers = AL.GenBuffers(4);
		_thread.Start();
	
		IsInitialized = true;
		return ExitCode.Success;
	}

	public void Close()
	{
		IsInitialized = false;
		
		_cts.Cancel();
		_thread.Join();
		_cts.Dispose();
		
		_packets.Clear();
		
		AL.SourceStop(_source);
		AL.DeleteSource(_source);
		AL.DeleteBuffers(_buffers);

		ALContext context = ALC.GetCurrentContext();
		ALDevice device = ALC.GetContextsDevice(context);
		ALC.DestroyContext(context);
		ALC.CloseDevice(device);
		
		_cts = null!;
		_thread = null!;
		_buffers = null!;
	}

	public void EnqueuePacket(byte[] packet)
	{
		if (!IsInitialized) return;
		
		if (packet.Length > SharedDefinitions.AudioBytesPerPacket)
		{
			byte[] correct = new byte[SharedDefinitions.AudioBytesPerPacket];
			Array.Copy(packet, correct, SharedDefinitions.AudioBytesPerPacket);
			_packets.Enqueue(correct);
		}
		else if (packet.Length < SharedDefinitions.AudioBytesPerPacket)
		{
			byte[] correct = new byte[SharedDefinitions.AudioBytesPerPacket];
			Array.Copy(packet, correct, packet.Length);
			_packets.Enqueue(correct);
		}
		else
		{
			_packets.Enqueue(packet);
		}
	}

	private void AudioPlayer()
	{
		int framesPerPacket = (int)((float)SharedDefinitions.AudioFramesFrequency * ((float)SharedDefinitions.AudioPacketMs / 1000.0));
		int bytesPerPacket = framesPerPacket * 2 * 2;	/* Using two channels, s16le */
		
		byte[] silence = new byte[bytesPerPacket];

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

				if (_packets.TryDequeue(out byte[]? packet))
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