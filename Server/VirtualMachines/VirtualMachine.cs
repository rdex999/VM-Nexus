using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Input;
using libvirt;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Protocol.SecurityTypes;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging;
using Server.Drives;
using Server.Services;
using Shared;
using Shared.VirtualMachines;
using PixelFormat = MarcusW.VncClient.PixelFormat;
using Rectangle = MarcusW.VncClient.Rectangle;
using Size = MarcusW.VncClient.Size;

namespace Server.VirtualMachines;

public class VirtualMachine
{
	public event EventHandler<int>? PoweredOff;
	public event EventHandler<int>? Crashed;
	public event EventHandler<VirtualMachineFrame>? FrameReceived;
	public event EventHandler<byte[]>? AudioPacketReceived;					/* Gives the packet byte array - representing frames. (two channels, s16le) */
	public readonly TaskCompletionSource<virDomainState> PoweredOffTcs;		/* Returns the new state - powered off, crashed */
	
	public static readonly TimeSpan PowerOffTimeout = TimeSpan.FromMinutes(1);
	public int Id { get; }
	
	private readonly DatabaseService _databaseService;
	private readonly DriveService _driveService;
	private Domain _libvirtDomain = null!;
	private RfbConnection _rfbConnection = null!;
	private readonly CancellationTokenSource _cts;
	private readonly CancellationTokenSource _rfbMessageCts;

	private readonly SharedDefinitions.OperatingSystem _operatingSystem;
	private readonly CpuArchitecture _cpuArchitecture;
	private readonly SharedDefinitions.BootMode _bootMode;
	private readonly DriveDescriptor[] _drives;
	private int _pointerPressedButtons = (int)SharedDefinitions.MouseButtons.None;
	private bool _isLeftShiftKeyDown = false;
	private bool _isRightShiftKeyDown = false;
	private readonly string _pcmAudioFilePath;
	private int _fps = 20;
	private readonly object _frameLock;
	private VirtualMachineFrame? _lastFrame = null;
	private Task _allBackgroundTasks = null!;
	private bool _closing = false;
	
	public VirtualMachine(DatabaseService databaseService, DriveService driveService, int id, SharedDefinitions.OperatingSystem operatingSystem,
		CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode, DriveDescriptor[] drives)
	{
		_databaseService = databaseService;
		_driveService = driveService;
		Id = id;
		_operatingSystem = operatingSystem;
		_drives = drives;
		_cpuArchitecture = cpuArchitecture;
		_bootMode = bootMode;
		_pcmAudioFilePath = $"/tmp/VM_Nexus_vm_{Id}.pcm";

		PoweredOffTcs = new TaskCompletionSource<virDomainState>();
		_cts = new CancellationTokenSource();
		_rfbMessageCts = new CancellationTokenSource();
		_frameLock = new object();
	}

	/// <summary>
	/// Closes this instance of the virtual machine interface.
	/// If the virtual machine is not already shut down, a graceful shutdown will be attempted,
	/// after a timeout the virtual machine will be forced off.
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine was shutdown/crashed, or closing it is required. <br/>
	/// Postcondition: The virtual machine is powered off, the connection to the virtual machine is closed, (VNC) resources are freed.
	/// </remarks>
	public async Task CloseAsync()
	{
		if (_closing)
		{
			return;
		}
		_closing = true;

		await _rfbMessageCts.CancelAsync();
		
		if (GetVmState() == SharedDefinitions.VmState.Running)
		{
			await PowerOffAndDestroyOnTimeoutAsync();
		}
		
		StopScreenStream();
		
		await _cts.CancelAsync();
		
		await _allBackgroundTasks;
		
		try
		{
			await _rfbConnection.CloseAsync();
			_rfbConnection.Dispose();
		}
		catch (OperationCanceledException)
		{
			// ignored
		}
		catch (Exception)
		{
			// ignored
		}
		
		_rfbMessageCts.Dispose();
		_cts.Dispose();

		await using FileStream fileStream = new FileStream(_pcmAudioFilePath, FileMode.Truncate);
	}

	/// <summary>
	/// Powers on this virtual machine.
	/// </summary>
	/// <param name="libvirtConnection">The libvirt connection. libvirtConnection != null.</param>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The virtual machine is powered off. libvirtConnection is connected. libvirtConnection != null. <br/>
	/// Postcondition: On success, the virtual machine is powered on, and the returned exit code will indicate success. <br/>
	/// On failure, the virtual machine is not powered on, and the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> PowerOnAsync(Connect libvirtConnection)
	{
		// int status = mkfifo(_pcmAudioFilePath, 511);
		// if (status != 0) return ExitCode.NamedPipeCreationFailed;

		string xml = AsLibvirtDomainXml().ToString();
		try
		{
			_libvirtDomain = libvirtConnection.CreateDomain(xml);
		}
		catch (Exception)
		{
			return ExitCode.VmStartupFailed;
		}
		
		XDocument xmlDoc = XDocument.Parse(_libvirtDomain.Xml);
		XElement? devices = xmlDoc.Descendants("devices").FirstOrDefault();
		if (devices == null) return ExitCode.InvalidVmXml;
		
		XElement? graphics = devices.Descendants("graphics").FirstOrDefault();
		if (graphics == null) return ExitCode.InvalidVmXml;
		
		XAttribute? vncPortAttr = graphics.Attributes("port").FirstOrDefault();
		if (vncPortAttr == null) return ExitCode.InvalidVmXml;
		
		if (!int.TryParse(vncPortAttr.Value, out var vncPort) || vncPort < 0 || vncPort > 65535) return ExitCode.InvalidVmXml;

		var loggerFactory = new LoggerFactory();
		VncClient vncClient = new VncClient(loggerFactory);
		try
		{
			_rfbConnection = await vncClient.ConnectAsync(new ConnectParameters()
			{
				TransportParameters = new TcpTransportParameters()
				{
					Host = "127.0.0.1",
					Port = vncPort
				},
				AllowSharedConnection = true,
				ConnectTimeout = TimeSpan.FromSeconds(3),
				RenderFlags = RenderFlags.Default,
				AuthenticationHandler = new VirtualMachineVncAuthenticationHandler(),
			});		/* Cancel after 3 seconds. */
		}
		catch (Exception)
		{
			_ = StateInformerAsync();
			await PowerOffAndDestroyOnTimeoutAsync();
			return ExitCode.VncConnectionFailed;
		}

		Task stateInformer = StateInformerAsync();
		Task frameSender = FrameSenderAsync();
		Task audioCaptureWorker = AudioCaptureWorkerAsync();
		
		_allBackgroundTasks = Task.WhenAll(stateInformer, frameSender, audioCaptureWorker);
		
		ExitCode result = StartScreenStream();
		if (result != ExitCode.Success)
		{
			await PowerOffAndDestroyOnTimeoutAsync();
			return result;
		}
		
		return await _databaseService.SetVmStateAsync(Id, SharedDefinitions.VmState.Running);
	}

	/// <summary>
	/// Attempts to gracefully power off the virtual machine. The virtual machine might ignore this signal.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The virtual machine is running, a power off is required. <br/>
	/// Postcondition: On success, the virtual machine is powered off and the returned exit code will indicate success. <br/>
	/// On failure, the virtual machine is not powered off, and the returned exit code will indicate the error.
	/// </remarks>
	public async Task<ExitCode> PowerOffAsync()
	{
		if (GetVmState() == SharedDefinitions.VmState.ShutDown) return ExitCode.VmIsShutDown;
		
		_libvirtDomain.Shutdown();

		try
		{
			virDomainState state = await PoweredOffTcs.Task.WaitAsync(PowerOffTimeout);
			if (state == virDomainState.VIR_DOMAIN_CRASHED)
			{
				return ExitCode.VmCrashed;
			}

			return ExitCode.Success;
		}
		catch (OperationCanceledException)
		{
			return ExitCode.VmShutdownTimeout;
		}
		catch (TimeoutException)
		{
			return ExitCode.VmShutdownTimeout;
		}
	}

	/// <summary>
	/// Attempts to gracefully power off the virtual machine.
	/// If the virtual machine ignores the shutdown signal, (not shutdown on timeout) it will be forced shutdown.
	/// </summary>
	/// <returns>
	/// An exit code indicating the result of the operation.
	/// If a forced shutdown was done, ExitCode.VmShutdownTimeout is returned.
	/// </returns>
	/// <remarks>
	/// Precondition: The virtual machine is powered on. It must be shutdown. <br/>
	/// Postcondition: The virtual machine is shutdown.
	/// If the virtual machine responded to the shutdown signal, and did in fact shutdown, ExitCode.Success is returned.
	/// If the graceful shutdown has timed out, the virtual machine is forced shut down (destroyed) and ExitCode.VmShutdownTimeout is returned.
	/// </remarks>
	public async Task<ExitCode> PowerOffAndDestroyOnTimeoutAsync()
	{
		ExitCode result = await PowerOffAsync();
		if (result == ExitCode.VmShutdownTimeout)
		{
			Destroy();
			return ExitCode.VmShutdownTimeout;
		}
		return result;
	}

	/// <summary>
	/// Destroys the virtual machine - forced shutdown.
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine is running, a forced shutdown is required. <br/>
	/// Postcondition: The virtual machine is destroyed.
	/// </remarks>
	public void Destroy() => _libvirtDomain.Destroy();

	/// <summary>
	/// Checks if the screen stream of the virtual machine is running.
	/// </summary>
	/// <returns>True if the stream is running, false otherwise.</returns>
	/// <remarks>
	/// Precondition: The virtual machine is running. <br/>
	/// Postcondition: Returns whether the screen stream of the virtual machine is running. (true = running, false = not running)
	/// </remarks>
	public bool IsScreenStreamRunning() => GetRenderTarget() != null;

	/// <summary>
	/// Enqueues a request to receive a full frame of the virtual machines screen. The frame will be received by the FrameReceived event.
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine is running, and its screen stream is running. <br/>
	/// Postcondition: The request is enqueued in the virtual machines message queue.
	/// </remarks>
	public void EnqueueGetFullFrame()
	{
		if (_closing || GetVmState() == SharedDefinitions.VmState.ShutDown) return;
		
		_rfbConnection.EnqueueMessage(
			new FramebufferUpdateRequestMessage(false, new Rectangle(0, 0, GetRenderTarget()!.ScreenSize.Width, GetRenderTarget()!.ScreenSize.Height)),
			_rfbMessageCts.Token
		);
	}

	/// <summary>
	/// Enqueues a pointer event message in the virtual machine, specifying the new location of the pointer on the virtual machines' screen.
	/// </summary>
	/// <param name="position">The new pointer position on the virtual machines' screen. Must be in valid range. position != null</param>
	/// <remarks>
	/// Precondition: The virtual machine is running, and its screen stream is running. The mouse position must be in valid range. position != null.<br/>
	/// Postcondition: The message is enqueued in the virtual machines message queue.
	/// </remarks>
	public void EnqueuePointerMovement(Point position)
	{
		if (_closing || GetVmState() == SharedDefinitions.VmState.ShutDown) return;
		
		_rfbConnection.EnqueueMessage(
			new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)_pointerPressedButtons),
			_rfbMessageCts.Token
		);
	}

	/// <summary>
	/// Enqueues a pointer event message in the virtual machine, specifying the new location of the pointer along with the new currently pressed mouse buttons.
	/// </summary>
	/// <param name="position">The new pointer position on the virtual machines' screen. Must be in valid range. position != null</param>
	/// <param name="pressedButtons">The currently pressed mouse buttons.</param>
	/// <remarks>
	/// Precondition: The virtual machine is running, and its screen stream is running. The mouse position must be in valid range. position != null.<br/>
	/// Postcondition: The message is enqueued in the virtual machines message queue.
	/// </remarks>
	public void EnqueuePointerButtonEvent(Point position, int pressedButtons)
	{
		if (_closing || GetVmState() == SharedDefinitions.VmState.ShutDown) return;
		
		if (pressedButtons == _pointerPressedButtons) return;
		
		_pointerPressedButtons = pressedButtons & ~(
			(int)SharedDefinitions.MouseButtons.WheelUp		| (int)SharedDefinitions.MouseButtons.WheelDown |
			(int)SharedDefinitions.MouseButtons.WheelLeft	| (int)SharedDefinitions.MouseButtons.WheelRight
		);
		
		/*
		 * If the mouse stayed at the same position but the wheel is still scrolling, VNC will ignore it. (because the buttons didn't actually change)
		 * So I send the pointer message, then send it again without mouse wheel buttons.
		 * When the mouse wheel is constantly scrolling, it will be sent with mouse will then without, repeating as long as the wheel scrolls.
		 * This results in VNC actually seeing the mouse wheel scroll event, and responding to it.
		 */
		_rfbConnection.EnqueueMessage(
			new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)pressedButtons),
			_rfbMessageCts.Token
		);
		_rfbConnection.EnqueueMessage(
			new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)_pointerPressedButtons),
			_rfbMessageCts.Token
		);
	}

	/// <summary>
	/// Enqueues a keyboard key event message in the virtual machine, specifying a key and whether it's pressed or not
	/// </summary>
	/// <param name="key">The physical keyboard key that was pressed or released.</param>
	/// <param name="pressed">Whether this is a key press or release event. (true = pressed, false = released)</param>
	/// <remarks>
	/// Precondition: The virtual machine is running, and its screen stream is running. <br/>
	/// Postcondition: The message is enqueued in the virtual machines message queue.
	/// </remarks>
	public void EnqueueKeyboardKeyEvent(PhysicalKey key, bool pressed)
	{
		if (_closing || GetVmState() == SharedDefinitions.VmState.ShutDown) return;
		
		if (PhysicalKeyToKeySymbol.TryGetValue(key, out KeySymbol keySymbol))
		{
			if (key == PhysicalKey.ShiftLeft)
			{
				_isLeftShiftKeyDown = pressed;
			}
			else if (key == PhysicalKey.ShiftRight)
			{
				_isRightShiftKeyDown = pressed;
			}

			if ((_isRightShiftKeyDown || _isLeftShiftKeyDown) && (int)keySymbol >= (int)KeySymbol.a && (int)keySymbol <= (int)KeySymbol.z)
			{
				keySymbol -= KeySymbol.a - KeySymbol.A;
			}
			_rfbConnection.EnqueueMessage(new KeyEventMessage(pressed, keySymbol), _rfbMessageCts.Token);
		}
	}

	/// <summary>
	/// Gets the used pixel format in the virtual machines' screen stream.
	/// </summary>
	/// <returns>The used pixel format, or null if the stream is not running.</returns>
	/// <remarks>
	/// Precondition: The virtual machine is running, and its screen stream is running. <br/>
	/// Postcondition: On success, the used pixel format for the screen stream is returned.
	/// On failure, (stream not running) null is returned.
	/// </remarks>
	public Shared.PixelFormat? GetScreenStreamPixelFormat()
	{
		if (!IsScreenStreamRunning()) return null;

		return GetRenderTarget()!.UniPixelFormat;
	}

	/// <summary>
	/// Starts the screen stream of the virtual machine.
	/// </summary>
	/// <returns>An exit code indicating the result of the operation.</returns>
	/// <remarks>
	/// Precondition: The virtual machine is running. VNC is connected. <br/>
	/// Postcondition: On success, the stream is started and the FrameReceived event will receive frames. <br/>
	/// On failure, the stream is not started and the returned exit code indicates the error.
	/// </remarks>
	private ExitCode StartScreenStream()
	{
		if (IsScreenStreamRunning())
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
		}

		try
		{
			_rfbConnection.RenderTarget = new VirtualMachineVncRenderTarget(Id, _rfbConnection.RemoteFramebufferFormat);
		}
		catch (Exception)
		{
			return ExitCode.VmScreenStreamUnsupportedPixelFormat;
		}

		GetRenderTarget()!.NewFrameReceived += (_, frame) =>
		{
			lock (_frameLock)
			{
				_lastFrame = frame;
			}
		};
			
		return ExitCode.Success;
	}

	/// <summary>
	/// Stops the screen stream of the virtual machine.
	/// </summary>
	/// <remarks>
	/// Precondition: Virtual machine is running, screen stream running. <br/>
	/// Postcondition: The screen stream is stopped, FrameReceived will not receive frames.
	/// </remarks>
	private void StopScreenStream() => _rfbConnection.RenderTarget = null;

	/// <summary>
	/// Handles state informing. Informs of state changed of the virtual machine. (powered off, crashed)
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine was powered on and is running. <br/>
	/// Postcondition: Virtual machine was powered off or crashed, events are invoked accordingly.
	/// </remarks>
	private async Task StateInformerAsync()
	{
		virDomainState lastState = GetState();
		while (!_cts.IsCancellationRequested)
		{
			virDomainState currentState = GetState();
			if (currentState != lastState)
			{
				switch (currentState)
				{
					case virDomainState.VIR_DOMAIN_SHUTOFF:		/* Machine is shut off. */
					case virDomainState.VIR_DOMAIN_SHUTDOWN:	/* Machine is being shut down (libvirts definition - not exactly accurate. Runs when the VM is shut down.) */
					{
						await _databaseService.SetVmStateAsync(Id, SharedDefinitions.VmState.ShutDown);
						PoweredOffTcs.SetResult(currentState);
						PoweredOff?.Invoke(this, Id);
						return;
					}
					case virDomainState.VIR_DOMAIN_CRASHED:
					{
						await _databaseService.SetVmStateAsync(Id, SharedDefinitions.VmState.ShutDown);
						PoweredOffTcs.SetResult(currentState);
						Crashed?.Invoke(this, Id);
						return;
					}
				}

				lastState = currentState;
			}
			
			try
			{
				await Task.Delay(200, _cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Sends screen frame received events.
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine was powered on, screen stream is running. <br/>
	/// Postcondition: While running, invokes screen frame received events if frames are received. Returns on virtual machine shutdown.
	/// </remarks>
	private async Task FrameSenderAsync()
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (!_cts.IsCancellationRequested)
		{
			stopwatch.Restart();
			VirtualMachineFrame? frame = null;
			lock (_frameLock)
			{
				if (_lastFrame != null)
				{
					frame = _lastFrame;
					_lastFrame = null;
				}
			}

			if (frame != null)
			{
				FrameReceived?.Invoke(this, frame);
			}

			try
			{
				await Task.Delay(Math.Max(0, (int)(1000 / _fps - stopwatch.ElapsedMilliseconds)), _cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Handles capturing the virtual machines' audio and sending it, by invoking the AudioPacketReceived event.
	/// </summary>
	/// <remarks>
	/// Precondition: The virtual machine was powered on - running. The file _pcmAudioFilePath exists. (Libvirt creates it on VM startup) <br/>
	/// Postcondition: While running, invokes the AudioPacketReceived for each audio packet that is received. Returns on virtual machine shutdown.
	/// </remarks>
	private async Task AudioCaptureWorkerAsync()
	{
		FileStream stream;
		try
		{
			stream = new FileStream(_pcmAudioFilePath, FileMode.Open, FileAccess.Read);
		}
		catch (Exception)
		{
			return;
		}

		/* Skip wave header - first 44 bytes */
		byte[] buffer = new byte[44];
		int bytesRead = 0;
		while (bytesRead < 44)
		{
			int currentRead;
			try
			{
				currentRead = await stream.ReadAsync(buffer, bytesRead, 44 - bytesRead, _cts.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { return; }

			bytesRead += currentRead;
		}

		buffer = new byte[SharedDefinitions.AudioBytesPerPacket];

		Stopwatch stopwatch = new Stopwatch();
		while (!_cts.IsCancellationRequested)
		{
			stopwatch.Restart();
			bytesRead = 0;
			while (bytesRead < SharedDefinitions.AudioBytesPerPacket)
			{
				int currentRead;
				try
				{
					currentRead = await stream
						.ReadAsync(buffer, bytesRead, SharedDefinitions.AudioBytesPerPacket - bytesRead, _cts.Token)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					return;
				}

				bytesRead += currentRead;

				/*
				 * If stopped receiving samples and the packet time has elapsed, (currentRead == 0 && stopwatch.ElapsedMilliseconds > SharedDefinitions.AudioPacketMs - 2)
				 * but we read some frames into the current packet, (bytesRead > 0) then fill the rest of the packet with silence and send it.
				 */
				if (stopwatch.ElapsedMilliseconds > SharedDefinitions.AudioPacketMs - 2 && currentRead == 0 && bytesRead > 0)
				{
					Array.Clear(buffer, bytesRead, buffer.Length - bytesRead);
					break;
				}
			}

			/* Do not send the packet if its silent. */
			if(!Common.IsAllBytesZeros(buffer))
			{
				if (stopwatch.ElapsedMilliseconds < SharedDefinitions.AudioPacketMs)
				{
					try
					{
						await Task.Delay(SharedDefinitions.AudioPacketMs - (int)stopwatch.ElapsedMilliseconds, _cts.Token).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}
				}
				AudioPacketReceived?.Invoke(this, buffer);
			}
		}
	}

	/// <summary>
	/// Get the state of the virtual machine. (Running, ShutDown)
	/// </summary>
	/// <returns>The state of the virtual machine.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: Returns the state of the virtual machine.
	/// </remarks>
	public SharedDefinitions.VmState GetVmState()
	{
		return GetState() switch
		{
			virDomainState.VIR_DOMAIN_CRASHED or virDomainState.VIR_DOMAIN_SHUTDOWN or virDomainState.VIR_DOMAIN_SHUTOFF => SharedDefinitions.VmState.ShutDown,
			_ => SharedDefinitions.VmState.Running,
		};
	}

	/// <summary>
	/// Get the state of the virtual machine, as a libvirt state. (virDomainState)
	/// </summary>
	/// <returns>The state of the virtual machine.</returns>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: The state of the virtual machine is returned.
	/// </remarks>
	private virDomainState GetState()
	{
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (_libvirtDomain == null) return virDomainState.VIR_DOMAIN_SHUTOFF;
		
		try
		{
			return _libvirtDomain.Info.State;
		}
		catch (LibvirtException)
		{
			return virDomainState.VIR_DOMAIN_SHUTOFF;
		}
	}

	/// <summary>
	/// Get libvirt domain XML of this virtual machine.
	/// </summary>
	/// <returns>An XML document, specifying a libvirt domain of this virtual machine.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if _cpuArchitecture contains an invalid or unsupported value.</exception>
	/// <remarks>
	/// Precondition: No specific precondition. <br/>
	/// Postcondition: A libvirt domain XML description of this virtual machine is returned.
	/// </remarks>
	private XDocument AsLibvirtDomainXml()
	{
		string cpuArch = _cpuArchitecture switch
		{
			CpuArchitecture.X86_64 => "x86_64",
			CpuArchitecture.X86 => "i686",
			CpuArchitecture.Arm => "ARM",
			_ => throw new ArgumentOutOfRangeException()
		};
		XElement os = new XElement("os",
			new XElement("type", "hvm", 
				new XAttribute("arch", cpuArch),
				new XAttribute("machine", "pc-q35-10.0")
			),
			new XElement("boot", new XAttribute("dev", "cdrom")),
			new XElement("boot", new XAttribute("dev", "hd")),
			new XElement("bootmenu", 
				new XAttribute("enable", "yes"), 
				new XAttribute("timeout", "2000")
			)
		);

		if (_bootMode == SharedDefinitions.BootMode.Bios)
		{
			// os.Add(new XElement("smbios", new XAttribute("mode", "sysinfo")));
		}
		else if (_bootMode == SharedDefinitions.BootMode.Uefi)
		{
			os.Add(new XAttribute("type", "efi"));
			os.Add(new XElement("loader", "/usr/share/edk2/x64/OVMF_CODE.4m.fd",
				new XAttribute("stateless", "yes"),
				new XAttribute("readonly", "yes"),
				new XAttribute("type", "pflash")
				)
			);
		}

		XElement devices = new XElement("devices",
			new XElement("input", new XAttribute("type", "keyboard"), new XAttribute("bus", "ps2")),
			new XElement("input", new XAttribute("type", "mouse"), new XAttribute("bus", "ps2")),
			new XElement("input", new XAttribute("type", "tablet"), new XAttribute("bus", "virtio")),
			new XElement("graphics",
				new XAttribute("type", "vnc"),
				new XAttribute("autoport", "yes")
			),
			new XElement("graphics", new XAttribute("type", "egl-headless")
				// new XElement("gl", new XAttribute("enabled", "yes"))
			),
			new XElement("interface", new XAttribute("type", "network"),
				new XElement("source", new XAttribute("network", "default")),
				new XElement("model", new XAttribute("type", "virtio"))
			),
			new XElement("sound", new XAttribute("model", "ich9"), 
				new XElement("audio", new XAttribute("id", "1"))
			),
			new XElement("audio", 
				new XAttribute("id", "1"), 
				new XAttribute("type", "file"), 
				new XAttribute("path", _pcmAudioFilePath),
				new XAttribute("format", "pcm"),
				new XElement("output", 
					new XAttribute("fixedSettings", "yes"), 
					new XAttribute("mixingEngine", "yes"), 
					// new XAttribute("bufferLength", AudioPacketMs.ToString()),
					
					new XElement("settings",
						new XAttribute("frequency", SharedDefinitions.AudioFramesFrequency.ToString()),
						new XAttribute("channels", SharedDefinitions.AudioChannels.ToString()),
						new XAttribute("format", "s16")
					)
				)
			)
		);

		if (_operatingSystem == SharedDefinitions.OperatingSystem.MiniCoffeeOS)
		{
			devices.Add(new XElement("model", new XAttribute("type", "vga")));
		}
		else
		{
			devices.Add(new XElement("video",
				new XElement("model",
					new XAttribute("type", "virtio"),
					new XAttribute("heads", "1"),
					new XAttribute("primary", "yes"),
					new XElement("acceleration",
						new XAttribute("accel3d", "yes")
					)
				)
			));
		}
		
		foreach (DriveDescriptor drive in _drives)
		{
			string driveFilePath = _driveService.GetDriveFilePath(drive.Id);
			XElement disk = new XElement("disk",
				new XAttribute("type", "file"),
				new XAttribute("device", drive.Type == Shared.Drives.DriveType.CDROM ? "cdrom" : "disk"),
				new XElement("driver", new XAttribute("name", "qemu"), new XAttribute("type", "raw")),
				new XElement("source", new XAttribute("file", driveFilePath)),
				new XElement("target", 
					new XAttribute("dev", drive.Type == Shared.Drives.DriveType.CDROM ? "sda" : "vda"),
					new XAttribute("bus", drive.Type == Shared.Drives.DriveType.CDROM ? "sata" : "virtio")
				)
			);
			if (drive.Type == Shared.Drives.DriveType.CDROM)
			{
				disk.Add(new XElement("readonly"));
			}
			devices.Add(disk);
		}
		
		XDocument doc = new XDocument(
			new XElement("domain", new XAttribute("type", "kvm"),
				new XElement("name", Id.ToString()),
				new XElement("memory", "8192", new XAttribute("unit", "MiB")),
				new XElement("features",
					new XElement("vmport", new XAttribute("state", "off")),
					new XElement("acpi"),
					new XElement("apic")
				),
				os,
				devices
			)
		);
		
		return doc;
	}

	/// <summary>
	/// Gets the render target of the VNC connection, as a VirtualMachineVncRenderTarget.
	/// </summary>
	/// <returns>The VNC render target, or null if no render target was set.</returns>
	/// <remarks>
	/// Precondition: The VNC render target should be set. _rfbConnection.RenderTarget != null. <br/>
	/// Postcondition: If the render target was set, it is returned as a VirtualMachineVncRenderTarget. Otherwise, null is returned.
	/// </remarks>
	private VirtualMachineVncRenderTarget? GetRenderTarget() => (VirtualMachineVncRenderTarget?)_rfbConnection.RenderTarget;
	
	private class VirtualMachineVncAuthenticationHandler : IAuthenticationHandler
	{
		/// <summary>
		/// Handles a VNC authentication request.
		/// </summary>
		/// <param name="connection">Unused.</param>
		/// <param name="securityType">Unused.</param>
		/// <param name="request">Unused.</param>
		/// <typeparam name="TInput">Must be PasswordAuthenticationInput.</typeparam>
		/// <returns>The authentication input needed to authenticate the VNC client.</returns>
		/// <exception cref="InvalidOperationException">Thrown if TInput is not PasswordAuthenticationInput.</exception>
		/// <remarks>
		/// Precondition: VNC client authentication is needed. TInput is PasswordAuthenticationInput. <br/>
		/// Postcondition: PasswordAuthenticationInput is returned.
		/// </remarks>
		public async Task<TInput> ProvideAuthenticationInputAsync<TInput>(RfbConnection connection, ISecurityType securityType, IAuthenticationInputRequest<TInput> request)
			where TInput : class, IAuthenticationInput
		{
			if (typeof(TInput) == typeof(PasswordAuthenticationInput))
			{
				return (TInput)Convert.ChangeType(new PasswordAuthenticationInput(string.Empty), typeof(TInput));
			}
			
			throw new InvalidOperationException("The authentication input request is not supported by this authentication handler.");
		}
	}
	
	private static readonly Dictionary<PhysicalKey, KeySymbol> PhysicalKeyToKeySymbol = new()
	{
		// Writing System Keys
		{ PhysicalKey.Backquote, KeySymbol.grave }, // `~
		{ PhysicalKey.Backslash, KeySymbol.backslash }, // \|
		{ PhysicalKey.BracketLeft, KeySymbol.bracketleft }, // [{
		{ PhysicalKey.BracketRight, KeySymbol.bracketright }, // ]}
		{ PhysicalKey.Comma, KeySymbol.comma }, // ,<
		{ PhysicalKey.Digit0, KeySymbol.XK_0 }, // 0)
		{ PhysicalKey.Digit1, KeySymbol.XK_1 }, // 1!
		{ PhysicalKey.Digit2, KeySymbol.XK_2 }, // 2@
		{ PhysicalKey.Digit3, KeySymbol.XK_3 }, // 3#
		{ PhysicalKey.Digit4, KeySymbol.XK_4 }, // 4$
		{ PhysicalKey.Digit5, KeySymbol.XK_5 }, // 5%
		{ PhysicalKey.Digit6, KeySymbol.XK_6 }, // 6^
		{ PhysicalKey.Digit7, KeySymbol.XK_7 }, // 7&
		{ PhysicalKey.Digit8, KeySymbol.XK_8 }, // 8*
		{ PhysicalKey.Digit9, KeySymbol.XK_9 }, // 9(
		{ PhysicalKey.Equal, KeySymbol.equal }, // =+
		{ PhysicalKey.IntlBackslash, KeySymbol.backslash }, // UK/ISO extra backslash key
		{ PhysicalKey.IntlRo, KeySymbol.kana_RO }, // Japanese Ro key
		{ PhysicalKey.IntlYen, KeySymbol.yen }, // Japanese Yen
		{ PhysicalKey.A, KeySymbol.a }, { PhysicalKey.B, KeySymbol.b }, { PhysicalKey.C, KeySymbol.c },
		{ PhysicalKey.D, KeySymbol.d }, { PhysicalKey.E, KeySymbol.e }, { PhysicalKey.F, KeySymbol.f },
		{ PhysicalKey.G, KeySymbol.g }, { PhysicalKey.H, KeySymbol.h }, { PhysicalKey.I, KeySymbol.i },
		{ PhysicalKey.J, KeySymbol.j }, { PhysicalKey.K, KeySymbol.k }, { PhysicalKey.L, KeySymbol.l },
		{ PhysicalKey.M, KeySymbol.m }, { PhysicalKey.N, KeySymbol.n }, { PhysicalKey.O, KeySymbol.o },
		{ PhysicalKey.P, KeySymbol.p }, { PhysicalKey.Q, KeySymbol.q }, { PhysicalKey.R, KeySymbol.r },
		{ PhysicalKey.S, KeySymbol.s }, { PhysicalKey.T, KeySymbol.t }, { PhysicalKey.U, KeySymbol.u },
		{ PhysicalKey.V, KeySymbol.v }, { PhysicalKey.W, KeySymbol.w }, { PhysicalKey.X, KeySymbol.x },
		{ PhysicalKey.Y, KeySymbol.y }, { PhysicalKey.Z, KeySymbol.z },
		{ PhysicalKey.Minus, KeySymbol.minus }, // -_
		{ PhysicalKey.Period, KeySymbol.period }, // .>
		{ PhysicalKey.Quote, KeySymbol.apostrophe }, // '"
		{ PhysicalKey.Semicolon, KeySymbol.semicolon }, // ;:
		{ PhysicalKey.Slash, KeySymbol.slash }, // /?

		// Functional keys
		{ PhysicalKey.AltLeft, KeySymbol.Alt_L },
		{ PhysicalKey.AltRight, KeySymbol.Alt_R },
		{ PhysicalKey.Backspace, KeySymbol.BackSpace },
		{ PhysicalKey.CapsLock, KeySymbol.Caps_Lock },
		{ PhysicalKey.ContextMenu, KeySymbol.Menu }, 
		{ PhysicalKey.ControlLeft, KeySymbol.Control_L },
		{ PhysicalKey.ControlRight, KeySymbol.Control_R },
		{ PhysicalKey.Enter, KeySymbol.Return },
		{ PhysicalKey.MetaLeft, KeySymbol.Super_L }, // Windows/Command key
		{ PhysicalKey.MetaRight, KeySymbol.Super_R },
		{ PhysicalKey.ShiftLeft, KeySymbol.Shift_L },
		{ PhysicalKey.ShiftRight, KeySymbol.Shift_R },
		{ PhysicalKey.Space, KeySymbol.space },
		{ PhysicalKey.Tab, KeySymbol.Tab },
		{ PhysicalKey.Escape, KeySymbol.Escape },

		// Control Pad
		{ PhysicalKey.Delete, KeySymbol.Delete },
		{ PhysicalKey.End, KeySymbol.End },
		{ PhysicalKey.Help, KeySymbol.Help },
		{ PhysicalKey.Home, KeySymbol.Home },
		{ PhysicalKey.Insert, KeySymbol.Insert },
		{ PhysicalKey.PageDown, KeySymbol.Next },
		{ PhysicalKey.PageUp, KeySymbol.Prior },

		// Arrow Pad
		{ PhysicalKey.ArrowDown, KeySymbol.Down },
		{ PhysicalKey.ArrowLeft, KeySymbol.Left },
		{ PhysicalKey.ArrowRight, KeySymbol.Right },
		{ PhysicalKey.ArrowUp, KeySymbol.Up },

		// Numeric Keypad
		{ PhysicalKey.NumLock, KeySymbol.Num_Lock },
		{ PhysicalKey.NumPad0, KeySymbol.KP_0 },
		{ PhysicalKey.NumPad1, KeySymbol.KP_1 },
		{ PhysicalKey.NumPad2, KeySymbol.KP_2 },
		{ PhysicalKey.NumPad3, KeySymbol.KP_3 },
		{ PhysicalKey.NumPad4, KeySymbol.KP_4 },
		{ PhysicalKey.NumPad5, KeySymbol.KP_5 },
		{ PhysicalKey.NumPad6, KeySymbol.KP_6 },
		{ PhysicalKey.NumPad7, KeySymbol.KP_7 },
		{ PhysicalKey.NumPad8, KeySymbol.KP_8 },
		{ PhysicalKey.NumPad9, KeySymbol.KP_9 },
		{ PhysicalKey.NumPadAdd, KeySymbol.KP_Add },
		{ PhysicalKey.NumPadClear, KeySymbol.KP_Begin },
		{ PhysicalKey.NumPadComma, KeySymbol.KP_Separator },
		{ PhysicalKey.NumPadDecimal, KeySymbol.KP_Decimal },
		{ PhysicalKey.NumPadDivide, KeySymbol.KP_Divide },
		{ PhysicalKey.NumPadEnter, KeySymbol.KP_Enter },
		{ PhysicalKey.NumPadEqual, KeySymbol.KP_Equal },
		{ PhysicalKey.NumPadMultiply, KeySymbol.KP_Multiply },
		{ PhysicalKey.NumPadSubtract, KeySymbol.KP_Subtract },

		// Function keys
		{ PhysicalKey.F1, KeySymbol.F1 }, { PhysicalKey.F2, KeySymbol.F2 },
		{ PhysicalKey.F3, KeySymbol.F3 }, { PhysicalKey.F4, KeySymbol.F4 },
		{ PhysicalKey.F5, KeySymbol.F5 }, { PhysicalKey.F6, KeySymbol.F6 },
		{ PhysicalKey.F7, KeySymbol.F7 }, { PhysicalKey.F8, KeySymbol.F8 },
		{ PhysicalKey.F9, KeySymbol.F9 }, { PhysicalKey.F10, KeySymbol.F10 },
		{ PhysicalKey.F11, KeySymbol.F11 }, { PhysicalKey.F12, KeySymbol.F12 },
		{ PhysicalKey.F13, KeySymbol.F13 }, { PhysicalKey.F14, KeySymbol.F14 },
		{ PhysicalKey.F15, KeySymbol.F15 }, { PhysicalKey.F16, KeySymbol.F16 },
		{ PhysicalKey.F17, KeySymbol.F17 }, { PhysicalKey.F18, KeySymbol.F18 },
		{ PhysicalKey.F19, KeySymbol.F19 }, { PhysicalKey.F20, KeySymbol.F20 },
		{ PhysicalKey.F21, KeySymbol.F21 }, { PhysicalKey.F22, KeySymbol.F22 },
		{ PhysicalKey.F23, KeySymbol.F23 }, { PhysicalKey.F24, KeySymbol.F24 },

		{ PhysicalKey.PrintScreen, KeySymbol.Print },
		{ PhysicalKey.ScrollLock, KeySymbol.Scroll_Lock },
		{ PhysicalKey.Pause, KeySymbol.Pause },
	};
	
	[DllImport("libc", SetLastError = true)]
	private static extern int mkfifo(string pathname, uint mode);

	private class VirtualMachineVncRenderTarget : IRenderTarget
	{
		public event EventHandler<VirtualMachineFrame>? NewFrameReceived;
		private readonly int _vmId;
		private byte[]? _framebuffer;
		private readonly object _lock;
		public Size ScreenSize { get; private set; }
		private readonly PixelFormat _pixelFormat;
		public Shared.PixelFormat UniPixelFormat { get; } /* Universal pixel format */
		private bool _grabbed = false;
		private GCHandle? _framebufferHandle;

		public VirtualMachineVncRenderTarget(int vmId, PixelFormat pixelFormat)
		{
			_vmId = vmId;
			_pixelFormat = pixelFormat;
			UniPixelFormat = new Shared.PixelFormat(_pixelFormat.BitsPerPixel, _pixelFormat.HasAlpha, _pixelFormat.RedShift,
				_pixelFormat.GreenShift, _pixelFormat.BlueShift, _pixelFormat.AlphaShift);

			_lock = new object();
			ScreenSize = new Size(0, 0);
		}

		/// <summary>
		/// Called by the VncClient library, to write into the framebuffer. Returns a reference to the framebuffer.
		/// </summary>
		/// <param name="size">The size of the needed framebuffer, in pixels. size != null.</param>
		/// <param name="layout">Unused.</param>
		/// <returns>An IFramebufferReference, referencing the framebuffer.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the framebuffer is already grabbed - already is use.</exception>
		/// <remarks>
		/// Precondition: VNC has an update and needs to write changes into the framebuffer.
		/// (For example, the content of the virtual machines' screen has changed) size != null.<br/>
		/// Postcondition: An IFramebufferReference is returned, referencing the framebuffer.
		/// </remarks>
		public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
		{
			if (_grabbed)
			{
				throw new InvalidOperationException("Framebuffer is already grabbed.");
			}

			_grabbed = true;

			lock (_lock)
			{
				if (_framebuffer == null || ScreenSize.Width * ScreenSize.Height != size.Width * size.Height)
				{
					ScreenSize = size;
					_framebuffer = new byte[ScreenSize.Width * ScreenSize.Height * _pixelFormat.BytesPerPixel];
				}

				_framebufferHandle = GCHandle.Alloc(_framebuffer, GCHandleType.Pinned);
				return new FramebufferReference(_framebufferHandle.Value.AddrOfPinnedObject(), ScreenSize, _pixelFormat)
				{
					Released = OnFramebufferReleased
				};
			}
		}

		/// <summary>
		/// Handles the framebuffer being released - means it was updated with new content.
		/// (The screen of the virtual machine has changed, the framebuffer is updated to it)
		/// </summary>
		/// <remarks>
		/// Precondition: The framebuffer was released, and has new content in it. <br/>
		/// Postcondition: Notifies that a new frame was received. (Invokes the NewFrameReceived event)
		/// </remarks>
		private void OnFramebufferReleased()
		{
			_framebufferHandle!.Value.Free();

			byte[] compressed;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BrotliStream brotliStream = new BrotliStream(stream, CompressionLevel.Fastest, true))
				{
					brotliStream.Write(_framebuffer.AsSpan());
				}

				compressed = stream.ToArray();
			}

			NewFrameReceived?.Invoke(this, new VirtualMachineFrame(_vmId, new System.Drawing.Size(ScreenSize.Width, ScreenSize.Height), compressed));
			_grabbed = false;
		}

		private class FramebufferReference : IFramebufferReference
		{
			public IntPtr Address { get; }
			public Size Size { get; }
			public PixelFormat Format { get; }
			public double HorizontalDpi { get; set; }
			public double VerticalDpi { get; set; }
			public Action? Released { get; init; }

			public FramebufferReference(IntPtr address, Size size, PixelFormat format)
			{
				Address = address;
				Size = size;
				Format = format;
			}

			/// <summary>
			/// Disposes the framebuffer reference, notifies of the framebuffer being released.
			/// </summary>
			/// <remarks>
			/// Precondition: The framebuffer reference was released. <br/>
			/// Postcondition: The framebuffer reference is disposed, the Released event is invoked.
			/// </remarks>
			public void Dispose() => Released?.Invoke();
		}
	}
}

