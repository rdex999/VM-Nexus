using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Shared;
using Shared.Networking;
using Shared.VirtualMachines;
using OperatingSystem = System.OperatingSystem;
using PixelFormat = Avalonia.Platform.PixelFormat;
using Point = System.Drawing.Point;

namespace Client.ViewModels;

public partial class VmScreenViewModel : ViewModelBase
{
	public Action? NewFrameReceived;
	public Action<int, int>? VmFramebufferSizeChanged;

	private bool _isFocused = false;
	private bool _streamRunning = false;
	private VmGeneralDescriptor? _vmDescriptor = null;
	private PixelFormat? _pixelFormat = null;
	private readonly Stopwatch _pointerMovementStopwatch = new Stopwatch();
	private const int PointerMovementHz = 30;
	private bool _capsLockOn = false;
	private bool _numLockOn = false;
	private bool _scrollLockOn = false;
	private readonly PcmAudioPlayerService _audioPlayerService;
	
	[ObservableProperty] 
	private WriteableBitmap? _vmScreenBitmap = null;
	
	public VmScreenViewModel(NavigationService navigationSvc, ClientService clientSvc)
		: base(navigationSvc, clientSvc)
	{
		_audioPlayerService = new PcmAudioPlayerService();

		if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.ShutdownRequested += OnShutdownRequested;
		}
		
		ClientSvc.VmScreenFrameReceived += OnVmScreenFrameReceived;
		ClientSvc.VmAudioPacketReceived += OnVmAudioPacketReceived;
		ClientSvc.VmPoweredOn += OnVmPoweredOn;
		ClientSvc.VmPoweredOff += OnVmPoweredOff;
		ClientSvc.VmCrashed += OnVmCrashed;
	}

	/// <summary>
	/// Resumes the screen stream.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The user has clicked on the screen page, (VM screen) or selected another tab. <br/>
	/// Postcondition: If there is a virtual machine configured, (_vmDescriptor != null) then the stream is resumed. If there was non, no action is taken.
	/// </remarks>
	public async Task<ExitCode> FocusAsync()
	{
		_isFocused = true;
		if (!_streamRunning && _vmDescriptor != null && _vmDescriptor.State == VmState.Running)
		{
			return await StartStreamAsync();
		}
		return ExitCode.Success;
	}

	/// <summary>
	/// Stops the screen stream. (To save bandwidth)
	/// </summary>
	/// <remarks>
	/// Precondition: User has quit the screen page. (selected another side menu page) <br/>
	/// Postcondition: If there was a stream running, it is stopped. If there was not, no action is taken.
	/// </remarks>
	public async Task UnfocusAsync()
	{
		_isFocused = false;
		if (_streamRunning)
		{
			await EndStreamAsync();
		}
	}

	/// <summary>
	/// Switches the screen stream to stream the screen of another virtual machine.
	/// </summary>
	/// <param name="vmDescriptor">A descriptor of the new virtual machine to stream the screen of. vmDescriptor != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: User has selected another tab, or has selected the VM screen page. The given virtual machine must not be the same as the current one.
	/// vmDescriptor != null <br/>
	/// Postcondition: On success, the stream is started and the returned exit code indicates success. <br/>
	/// On failure, the stream is not started and the returned exit code indicates the error.
	/// </remarks>
	public async Task<ExitCode> SwitchVirtualMachineAsync(VmGeneralDescriptor vmDescriptor)
	{
		if (_vmDescriptor != null && _vmDescriptor.Id == vmDescriptor.Id)
		{
			return ExitCode.CallOnInvalidCondition;
		}

		_streamRunning = false;
		_vmDescriptor = vmDescriptor;
		VmScreenBitmap = null;

		if (_vmDescriptor.State == VmState.Running)
		{
			return await StartStreamAsync();
		}

		return ExitCode.Success;
	}

	/// <summary>
	/// Starts a screen stream of the current virtual machine.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine configured, (_vmDescriptor != null) no stream is running, and the virtual machine is running. <br/>
	/// Postcondition: On success, a new stream of the virtual machines' screen is started, and the returned exit code indicates success. <br/>
	/// On failure, the stream is not started and the returned exit code indicates the error.
	/// </remarks>
	private async Task<ExitCode> StartStreamAsync()
	{
		if (_streamRunning)
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
		}

		if (_vmDescriptor == null)
		{
			return ExitCode.CallOnInvalidCondition;
		}

		if (_vmDescriptor.State == VmState.ShutDown)
		{
			return ExitCode.VmIsShutDown;
		}
	
		_audioPlayerService.Initialize();
		MessageResponseVmStreamStart? response = await ClientSvc.VirtualMachineStartStreamAsync(_vmDescriptor!.Id);

		if (response == null)
		{
			_streamRunning = false;
			return ExitCode.MessageNotReceived;
		}
		
		if (response.Result == MessageResponseVmStreamStart.Status.Success || response.Result == MessageResponseVmStreamStart.Status.AlreadyStreaming)
		{
			_streamRunning = true;

			PixelFormat? pixelFormat = response.PixelFormat!.AsAvaloniaPixelFormat();
			if (pixelFormat == null)
			{
				return ExitCode.VmScreenStreamUnsupportedPixelFormat;
			}

			_pixelFormat = pixelFormat.Value;

			if (VmScreenBitmap != null)
			{
				/*
				 * If this view model is not focused, the code-behind is closed - thus it is re-created when focused,
				 * which means all of its properties are reset to default values, including its framebuffer size. (reset to 0)
				 * So tell it there is a size for it, and give it the size.
				 */
				VmFramebufferSizeChanged?.Invoke(VmScreenBitmap.PixelSize.Width, VmScreenBitmap.PixelSize.Height);
			}

			return ExitCode.Success;
		}

		return ExitCode.VmScreenStreamStartFailed;
	}

	/// <summary>
	/// Ends the current stream of the virtual machines' screen.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: There is a virtual machine configured (_vmDescriptor != null) and stream is running. <br/>
	/// Postcondition: On success, the stream is stopped and the returned exit code indicates success. <br/>
	/// If the server for some reason refuses to stop the stream, any incoming frames will be ignored. In this case the exit code will state VmScreenStreamStopFailed. <br/>
	/// On other failure, the stream is not stopped and the returned exit code will indicate the error.
	/// </remarks>
	private async Task<ExitCode> EndStreamAsync()
	{
		if (!_streamRunning)
		{
			return ExitCode.VmScreenStreamNotRunning;
		}

		if (_vmDescriptor == null)
		{
			return ExitCode.CallOnInvalidCondition;
		}
		
		_streamRunning = false;
		_audioPlayerService.Close();
		MessageResponseVmStreamStop.Status result = await ClientSvc.VirtualMachineStopStreamAsync(_vmDescriptor!.Id);
		if (result == MessageResponseVmStreamStop.Status.Success)
		{
			return ExitCode.Success;
		}

		if (result == MessageResponseVmStreamStop.Status.StreamNotRunning)
		{
			return ExitCode.VmScreenStreamNotRunning;
		}
		
		return ExitCode.VmScreenStreamStopFailed;
	}
	
	/// <summary>
	/// Runs when a new frame of a virtual machine is received.
	/// </summary>
	/// <param name="sender">Unused</param>
	/// <param name="frame">The frame that was received. frame != null.</param>
	/// <remarks>
	/// Precondition: A new frame of the screen of a virtual machine has been received. frame != null. <br/>
	/// Postcondition: The frame is rendered on the screen.
	/// </remarks>
	private void OnVmScreenFrameReceived(object? sender, MessageInfoVmScreenFrame frame)
	{
		if (!_streamRunning || _vmDescriptor == null || frame.VmId != _vmDescriptor.Id || _pixelFormat == null)
		{
			return;
		}

		if (VmScreenBitmap == null || VmScreenBitmap.PixelSize.Width * VmScreenBitmap.PixelSize.Height != frame.Size.Width * frame.Size.Height)
		{
			VmFramebufferSizeChanged?.Invoke(frame.Size.Width, frame.Size.Height);
			VmScreenBitmap = new WriteableBitmap(new PixelSize(frame.Size.Width, frame.Size.Height), new Vector(96, 96), _pixelFormat);
		}

		using MemoryStream output = new MemoryStream();
		if (OperatingSystem.IsBrowser())
		{
			/* TODO: Find out how to decompress this. */
		}
		else
		{
			using MemoryStream input = new MemoryStream(frame.CompressedFramebuffer);
			using (BrotliStream brotliStream = new BrotliStream(input, CompressionMode.Decompress))
			{
				brotliStream.CopyTo(output);
			}	
		}
		
		byte[] framebuffer = output.ToArray();

		using ILockedFramebuffer buffer = VmScreenBitmap.Lock();
		Marshal.Copy(framebuffer, 0, buffer.Address, frame.Size.Width * frame.Size.Height * (_pixelFormat.Value.BitsPerPixel / 8));
		Dispatcher.UIThread.Invoke(NewFrameReceived!);
	}

	/// <summary>
	/// Runs when a new audio packet is received from a virtual machine.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="packet">The audio packet information message. packet != null.</param>
	/// <remarks>
	/// Precondition: A new audio packet from a virtual machine was received. packet != null. <br/>
	/// Postcondition: The audio packet is played if conditions are met.
	/// </remarks>
	private void OnVmAudioPacketReceived(object? sender, MessageInfoVmAudioPacket packet)
	{
		if (_vmDescriptor == null || _vmDescriptor.Id != packet.VmId) return;
		
		_audioPlayerService.EnqueuePacket(packet.Packet);
	}

	/// <summary>
	/// Handles the event of the virtual machine being powered on.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered on. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine was powered on. id >= 1. <br/>
	/// Postcondition: Event is handled, screen stream started if needed.
	/// </remarks>
	private void OnVmPoweredOn(object? sender, int id)
	{
		if (_vmDescriptor == null || _vmDescriptor.Id != id)
		{
			return;
		}

		_vmDescriptor.State = VmState.Running;

		if (_isFocused)
		{
			_ = StartStreamAsync();
		}
	}
	
	/// <summary>
	/// Handles the event of the virtual machine being powered off.
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that was powered off. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine was powered off. id >= 1. <br/>
	/// Postcondition: Event is handled, screen stream stopped if needed.
	/// </remarks>
	private void OnVmPoweredOff(object? sender, int id)
	{
		if (_vmDescriptor == null || _vmDescriptor.Id != id)
		{
			return;
		}

		_vmDescriptor.State = VmState.ShutDown;
		
		VmScreenBitmap = null;
		if (_streamRunning)
		{
			_streamRunning = false;
			_audioPlayerService.Close();
		}
	}
	
	/// <summary>
	/// Handles the event of the virtual machine crashing
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="id">The ID of the virtual machine that has crashed. id >= 1.</param>
	/// <remarks>
	/// Precondition: The virtual machine has crashed. id >= 1. <br/>
	/// Postcondition: Event is handled, screen stream stopped if needed.
	/// </remarks>
	private void OnVmCrashed(object? sender, int id)
	{
		if (_vmDescriptor == null || _vmDescriptor.Id != id)
		{
			return;
		}

		OnVmPoweredOff(sender, id);
		
		/* TODO: Display a message */
	}

	/// <summary>
	/// Handles a shutdown request. (For example, the user closes the window)
	/// </summary>
	/// <param name="sender">Unused.</param>
	/// <param name="shutdownRequestedEventArgs">Unused.</param>
	/// <remarks>
	/// Precondition: An application shutdown was requested. <br/>
	/// Postcondition: Video and audio streaming is terminated and application shutdown procedure is started.
	/// </remarks>
	private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs shutdownRequestedEventArgs)
	{
		if (!_streamRunning) return;
	
		shutdownRequestedEventArgs.Cancel = true;
		
		_streamRunning = false;
		ClientSvc.VmScreenFrameReceived -= OnVmScreenFrameReceived;
		ClientSvc.VmAudioPacketReceived -= OnVmAudioPacketReceived;
		_audioPlayerService.Close();

		Task.Run(() => ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).TryShutdown());
	}
	
	/// <summary>
	/// Handles a pointer (mouse) move.
	/// </summary>
	/// <param name="position">The pointer position on the screen of the virtual machine, in pixels. Components must be in valid range. position != null.</param>
	/// <remarks>
	/// Precondition: The mouse has moved on the screen of the virtual machine. position must be in valid range. position != null. <br/>
	/// Postcondition: The move is handled, the server is informed.
	/// </remarks>
	public void OnVmScreenPointerMoved(Point position)
	{
		if (!_streamRunning || _vmDescriptor == null) return;

		if (!_pointerMovementStopwatch.IsRunning ||
		    _pointerMovementStopwatch.Elapsed.TotalMilliseconds >= (1.0 / PointerMovementHz) * 1000.0)
		{
			_pointerMovementStopwatch.Restart();
			
			ClientSvc.NotifyPointerMovement(_vmDescriptor.Id, position);
		}
	}

	/// <summary>
	/// Handles a pointer (mouse) button press/release event.
	/// </summary>
	/// <param name="position">The pointer position on the screen of the virtual machine, in pixels. Components must be in valid range. position != null.</param>
	/// <param name="pressed">The mouse buttons that are pressed. Defined by MouseButtons</param>
	/// <remarks>
	/// Precondition: One or more of the mouses buttons have been pressed or released. position must be in valid range. position != null. <br/>
	/// Postcondition: The press/release is handled, the server is informed.
	/// </remarks>
	public void OnVmScreenPointerButtonEvent(Point position, int pressed)
	{
		if (!_streamRunning || _vmDescriptor == null) return;
		
		ClientSvc.NotifyPointerButtonEvent(_vmDescriptor.Id, position, pressed);
	}

	/// <summary>
	/// Handles a keyboard key event. (Key pressed or released)
	/// </summary>
	/// <param name="key">The key that was pressed/released.</param>
	/// <param name="keyDown">Indicates whether the key is pressed or released.</param>
	/// <remarks>
	/// Precondition: A key was pressed/released upon the screen of a virtual machine. <br/>
	/// Postcondition: The key event is handled, the server is informed.
	/// </remarks>
	public void OnVmScreenKeyEvent(PhysicalKey key, bool keyDown)
	{
		if (!_streamRunning || _vmDescriptor == null) return;

		switch (key)
		{
			case PhysicalKey.CapsLock:
			{
				if(keyDown) ClientSvc.NotifyKeyboardKeyEvent(_vmDescriptor.Id, key, !_capsLockOn);
				_capsLockOn ^= true;
				break;
			}
			case PhysicalKey.NumLock:
			{
				if(keyDown) ClientSvc.NotifyKeyboardKeyEvent(_vmDescriptor.Id, key, !_numLockOn);
				_numLockOn ^= true;
				break;
			}
			case PhysicalKey.ScrollLock:
			{
				if(keyDown) ClientSvc.NotifyKeyboardKeyEvent(_vmDescriptor.Id, key, !_scrollLockOn);
				_scrollLockOn ^= true;
				break;
			}
			default:
			{
				ClientSvc.NotifyKeyboardKeyEvent(_vmDescriptor.Id, key, keyDown);
				break;
			}
		}
	}
}