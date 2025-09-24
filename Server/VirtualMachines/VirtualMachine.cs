using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
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
using PixelFormat = MarcusW.VncClient.PixelFormat;
using Rectangle = MarcusW.VncClient.Rectangle;
using Size = MarcusW.VncClient.Size;

namespace Server.VirtualMachines;

public class VirtualMachine
{
	public event EventHandler<int>? PoweredOff;
	public event EventHandler<int>? Crashed;
	public readonly TaskCompletionSource<virDomainState> PoweredOffTcs = new TaskCompletionSource<virDomainState>();		/* Returns the new state - powered off, crashed */
	
	public static readonly TimeSpan PowerOffTimeout = TimeSpan.FromMinutes(1);
	
	private readonly DatabaseService _databaseService;
	private readonly DriveService _driveService;
	private Domain _domain = null!;
	private RfbConnection _rfbConnection;
	private readonly CancellationTokenSource _cts;
	private readonly int _id;
	private readonly SharedDefinitions.OperatingSystem _operatingSystem;
	private readonly SharedDefinitions.CpuArchitecture _cpuArchitecture;
	private readonly SharedDefinitions.BootMode _bootMode;
	private readonly DriveDescriptor[] _drives;
	private int _pointerPressedButtons = (int)SharedDefinitions.MouseButtons.None;

	public VirtualMachine(DatabaseService databaseService, DriveService driveService, int id, SharedDefinitions.OperatingSystem operatingSystem,
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode, DriveDescriptor[] drives)
	{
		_databaseService = databaseService;
		_driveService = driveService;
		_id = id;
		_operatingSystem = operatingSystem;
		_drives = drives;
		_cpuArchitecture = cpuArchitecture;
		_bootMode = bootMode;
		
		_cts = new CancellationTokenSource();
	}

	public async Task CloseAsync()
	{
		if (GetVmState() == SharedDefinitions.VmState.Running)
		{
			await PowerOffAndDestroyOnTimeout();
		}
		_cts.Cancel();
		_cts.Dispose();
		
		await _rfbConnection.CloseAsync();
		_rfbConnection.Dispose();
	}
	
	public async Task<ExitCode> PowerOnAsync(Connect libvirtConnection)
	{
		string xml = AsXmlDocument().ToString();
		try
		{
			_domain = libvirtConnection.CreateDomain(xml);
		}
		catch (Exception)
		{
			return ExitCode.VmStartupFailed;
		}
		
		XDocument xmlDoc = XDocument.Parse(_domain.Xml);
		XElement? devices = xmlDoc.Descendants("devices").FirstOrDefault();
		if (devices == null) return ExitCode.VmStartupFailed;
		
		XElement? graphics = devices.Descendants("graphics").FirstOrDefault();
		if (graphics == null) return ExitCode.VmStartupFailed;
		
		XAttribute? vncPortAttr = graphics.Attributes("port").FirstOrDefault();
		if (vncPortAttr == null) return ExitCode.VmStartupFailed;
		
		if (!int.TryParse(vncPortAttr.Value, out var vncPort) || vncPort < 0 || vncPort > 65535) return ExitCode.VmStartupFailed;

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
			}, new CancellationTokenSource(3000).Token);		/* Cancel after 3 seconds. */
		}
		catch (Exception)
		{
			_ = StateInformerAsync();
			await PowerOffAndDestroyOnTimeout();
			return ExitCode.VncConnectionFailed;
		}

		_ = StateInformerAsync();

		return await _databaseService.SetVmStateAsync(_id, SharedDefinitions.VmState.Running);
	}

	public async Task<ExitCode> PowerOffAsync()
	{
		_domain.Shutdown();

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
	}

	public async Task<ExitCode> PowerOffAndDestroyOnTimeout()
	{
		ExitCode result = await PowerOffAsync();
		if (result == ExitCode.VmShutdownTimeout)
		{
			Destroy();
			return ExitCode.VmShutdownTimeout;
		}
		return result;
	}

	public void Destroy()
	{
		_domain.Destroy();
	}

	public bool IsScreenStreamRunning() => GetRenderTarget() != null;
	
	public ExitCode SubscribeToNewFrameReceived(EventHandler<VirtualMachineFrame> handler)
	{
		if (!IsScreenStreamRunning())
		{
			ExitCode result = StartScreenStream();

			if (result != ExitCode.Success)
			{
				return result;
			}
		}
	
		GetRenderTarget()!.NewFrameReceived += handler;
		
		return ExitCode.Success;
	}

	public ExitCode UnsubscribeFromNewFrameReceived(EventHandler<VirtualMachineFrame> handler)
	{
		if (!IsScreenStreamRunning())
		{
			return ExitCode.VmScreenStreamNotRunning;
		}
	
		GetRenderTarget()!.NewFrameReceived -= handler;

		return ExitCode.Success;	
	}

	public void EnqueueGetFullFrame() => _rfbConnection.EnqueueMessage(
		new FramebufferUpdateRequestMessage(false,
			new Rectangle(0, 0, GetRenderTarget()!.ScreenSize.Width, GetRenderTarget()!.ScreenSize.Height))
	);

	public void EnqueuePointerMovement(Point position) => _rfbConnection.EnqueueMessage(
		new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)_pointerPressedButtons)
	);

	public void EnqueuePointerButtonEvent(Point position, int pressedButtons)
	{
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
			new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)pressedButtons)
		);
		_rfbConnection.EnqueueMessage(
			new PointerEventMessage(new Position(position.X, position.Y), (MouseButtons)_pointerPressedButtons)
		);
	}

	public void EnqueueKeyboardKeyEvent(PhysicalKey key, bool pressed)
	{
		if (PhysicalKeyToKeySymbol.TryGetValue(key, out KeySymbol keySymbol))
		{
			_rfbConnection.EnqueueMessage(new KeyEventMessage(pressed, keySymbol));
		}
	}

	public Shared.PixelFormat? GetScreenStreamPixelFormat()
	{
		if (!IsScreenStreamRunning())
		{
			return null;
		}

		return GetRenderTarget()!.UniPixelFormat;
	}

	private ExitCode StartScreenStream()
	{
		if (IsScreenStreamRunning())
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
		}

		try
		{
			_rfbConnection.RenderTarget = new VirtualMachineVncRenderTarget(_id, _rfbConnection.RemoteFramebufferFormat);
		}
		catch (Exception)
		{
			return ExitCode.VmScreenStreamUnsupportedPixelFormat;
		}
			
		return ExitCode.Success;
	}

	private void StopScreenStream() => _rfbConnection.RenderTarget = null;

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
						await _databaseService.SetVmStateAsync(_id, SharedDefinitions.VmState.ShutDown);
						PoweredOffTcs.SetResult(currentState);
						PoweredOff?.Invoke(this, _id);
						return;
					}
					case virDomainState.VIR_DOMAIN_CRASHED:
					{
						await _databaseService.SetVmStateAsync(_id, SharedDefinitions.VmState.ShutDown);
						PoweredOffTcs.SetResult(currentState);
						Crashed?.Invoke(this, _id);
						return;
					}
				}

				lastState = currentState;
			}
			
			try
			{
				await Task.Delay(200, _cts.Token);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	public SharedDefinitions.VmState GetVmState()
	{
		return GetState() switch
		{
			virDomainState.VIR_DOMAIN_CRASHED or virDomainState.VIR_DOMAIN_SHUTDOWN or virDomainState.VIR_DOMAIN_SHUTOFF => SharedDefinitions.VmState.ShutDown,
			_ => SharedDefinitions.VmState.Running,
		};
	}
	
	private virDomainState GetState()
	{
		try
		{
			return _domain.Info.State;
		}
		catch (LibvirtException)
		{
			return virDomainState.VIR_DOMAIN_SHUTOFF;
		}
	}
	
	private XDocument AsXmlDocument()
	{
		string cpuArch = _cpuArchitecture switch
		{
			SharedDefinitions.CpuArchitecture.X86_64 => "x86_64",
			SharedDefinitions.CpuArchitecture.X86 => "i686",
			SharedDefinitions.CpuArchitecture.Arm => "ARM",
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
				// new XAttribute("port", "5900")	/* Temporary */
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
					new XAttribute("primary", "yes")
				)
			));
		}
		
		foreach (DriveDescriptor drive in _drives)
		{
			string driveFilePath = _driveService.GetDriveFilePath(drive.Id);
			XElement disk = new XElement("disk",
				new XAttribute("type", "file"),
				new XAttribute("device", drive.Type == SharedDefinitions.DriveType.CDROM ? "cdrom" : "disk"),
				new XElement("driver", new XAttribute("name", "qemu"), new XAttribute("type", "raw")),
				new XElement("source", new XAttribute("file", driveFilePath)),
				new XElement("target", 
					new XAttribute("dev", drive.Type == SharedDefinitions.DriveType.CDROM ? "sda" : "vda"),
					new XAttribute("bus", drive.Type == SharedDefinitions.DriveType.CDROM ? "sata" : "virtio")
				)
			);
			if (drive.Type == SharedDefinitions.DriveType.CDROM)
			{
				disk.Add(new XElement("readonly"));
			}
			devices.Add(disk);
		}
		
		XDocument doc = new XDocument(
			new XElement("domain", new XAttribute("type", "kvm"),
				new XElement("name", _id.ToString()),
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

	private VirtualMachineVncRenderTarget? GetRenderTarget() => (VirtualMachineVncRenderTarget?)_rfbConnection.RenderTarget;
	
	private class VirtualMachineVncAuthenticationHandler : IAuthenticationHandler
	{
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
		// { PhysicalKey.NumPadParenLeft, KeySymbol.VoidSymbol }, // Not in your enum
		// { PhysicalKey.NumPadParenRight, KeySymbol.VoidSymbol },
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
}

public class VirtualMachineVncRenderTarget : IRenderTarget
{
	public event EventHandler<VirtualMachineFrame>? NewFrameReceived;
	private int _vmId;
	private byte[]? _framebuffer;
	private readonly object _lock;
	public Size ScreenSize { get; private set; }
	private PixelFormat _pixelFormat;
	public Shared.PixelFormat UniPixelFormat { get; }		/* Universal pixel format */
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
	
	public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
	{
		if (_grabbed)
		{
			throw new InvalidOperationException("Framebuffer is already grabbed.");
		}

		_grabbed = true;
		
		lock (_lock)
		{
			if (_framebuffer == null || ScreenSize != size)
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

	public bool HasNewFrameReceivedSubscribers()
	{
		if (NewFrameReceived == null)
		{
			return false;
		}
		
		return NewFrameReceived.GetInvocationList().Length > 0;
	}

	private void OnFramebufferReleased(FramebufferReference framebufferReference)
	{
		_framebufferHandle!.Value.Free();
		NewFrameReceived?.Invoke(this, new VirtualMachineFrame(_vmId, new System.Drawing.Size(ScreenSize.Width, ScreenSize.Height), _framebuffer!));
		_grabbed = false;
	}

	private class FramebufferReference : IFramebufferReference
	{
		public IntPtr Address { get; }
		public Size Size { get; }
		public PixelFormat Format { get; }
		public double HorizontalDpi { get; set; }
		public double VerticalDpi { get; set; }
		public Action<FramebufferReference>? Released { get; set; }

		public FramebufferReference(IntPtr address, Size size, PixelFormat format)
		{
			Address = address;
			Size = size;
			Format = format;
		}
		
		public void Dispose() => Released?.Invoke(this);
	}
}

