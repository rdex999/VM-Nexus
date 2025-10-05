using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenTK.Audio.OpenAL;
using Shared;

namespace Client.Services;

public class PcmAudioPlayerService
{
	public bool IsInitialized { get; private set; }
	private readonly ConcurrentQueue<byte[]> _packets;
	private CancellationTokenSource _cts = null!;
	private Thread _thread = null!;
	private int _source = -1;
	private int[] _buffers = null!;

	public PcmAudioPlayerService()
	{
		_packets = new ConcurrentQueue<byte[]>();
	}

	/// <summary>
	/// Initializes the service.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: Service uninitialized. <br/>
	/// Postcondition: On success, the service is initialized and the returned exit code indicates success. <br/>
	/// On failure, the service is not initialized and the returned exit code indicates the error.
	/// </remarks>
	public ExitCode Initialize()
	{
		if (IsInitialized) return ExitCode.AlreadyInitialized;
		
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
		_buffers = AL.GenBuffers(16);
		
		IsInitialized = true;
		_thread.Start();
	
		return ExitCode.Success;
	}

	/// <summary>
	/// Closes and uninitializes the service.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: Service uninitialized.
	/// </remarks>
	public void Close()
	{
		if (!IsInitialized) return;
		
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

	/// <summary>
	/// Enqueue an audio packet (encoded in two channel s16le format) in the packet play queue.
	/// </summary>
	/// <param name="packet">
	/// The audio packet. Must be in 2 channel s16le format.
	/// Should be of size SharedDefinitions.AudioBytesPerPacket, will be padded or truncated with silence if needed.
	/// </param>
	/// <remarks>
	/// Precondition: Service initialized. packet encoded in 2 channel s16le. packet of size SharedDefinitions.AudioBytesPerPacket. packet != null. <br/>
	/// Postcondition: Packet truncated/padded with silence if needed. Packet is enqueued in the packet play queue.
	/// </remarks>
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

	/// <summary>
	/// Plays audio packets from the packet play queue.
	/// </summary>
	/// <remarks>
	/// Precondition: Service initialized. <br/>
	/// Postcondition: While running, plays packets from the packet play queue. If the queue is empty, plays silence.
	/// Returns when the server closes. (Close() was called)
	/// </remarks>
	private void AudioPlayer()
	{
		byte[] silence = new byte[SharedDefinitions.AudioBytesPerPacket];

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

			_cts.Token.WaitHandle.WaitOne(10);
		}
	}
}