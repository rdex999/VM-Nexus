using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using libvirt;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Protocol.SecurityTypes;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging;
using Server.Drives;
using Server.Services;
using Shared;
using PixelFormat = MarcusW.VncClient.PixelFormat;
using Size = MarcusW.VncClient.Size;

namespace Server.VirtualMachines;

public class VirtualMachine
{
	private readonly DatabaseService _databaseService;
	private readonly DriveService _driveService;
	private Domain _domain = null!;
	
	private int _id;
	private SharedDefinitions.OperatingSystem _operatingSystem;
	private SharedDefinitions.CpuArchitecture _cpuArchitecture;
	private SharedDefinitions.BootMode _bootMode;
	private DriveDescriptor[] _drives;
	private RfbConnection _rfbConnection;

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
	
		var loggerFactory = new LoggerFactory();
		VncClient vncClient = new VncClient(loggerFactory);
			
		try
		{
			_rfbConnection = await vncClient.ConnectAsync(new ConnectParameters()
			{
				TransportParameters = new TcpTransportParameters()
				{
					Host = "127.0.0.1",
					Port = 5900		/* Temporary */
				},
				AllowSharedConnection = true,
				ConnectTimeout = TimeSpan.FromSeconds(3),
				RenderFlags = RenderFlags.Default,
				AuthenticationHandler = new VirtualMachineVncAuthenticationHandler(),
			}, new CancellationTokenSource(3000).Token);		/* Cancel after 3 seconds. */
		}
		catch (Exception)
		{
			/* TODO: Force shutdown the VM */
			return ExitCode.VncConnectionFailed;
		}

		return ExitCode.Success;
	}

	public void PowerOff()
	{
		_domain.Shutdown();
	}

	public ExitCode StartScreenStream(Action<VirtualMachineFrame> callback)
	{
		if (IsScreenStreamRunning())
		{
			return ExitCode.VmScreenStreamAlreadyRunning;
		}

		try
		{
			_rfbConnection.RenderTarget = new VirtualMachineVncRenderTarget(_id, _rfbConnection.RemoteFramebufferFormat, callback);
		}
		catch (Exception)
		{
			return ExitCode.VmScreenStreamUnsupportedPixelFormat;
		}
		
		return ExitCode.Success;
	}

	public void StopScreenStream()
	{
		_rfbConnection.RenderTarget = null;
	}
	
	public bool IsScreenStreamRunning() => _rfbConnection.RenderTarget != null;

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
			new XElement("input", new XAttribute("type", "mouse"), new XAttribute("bus", "ps2")),
			new XElement("input", new XAttribute("type", "keyboard"), new XAttribute("bus", "ps2")),
			new XElement("graphics",
				new XAttribute("type", "vnc"),
				new XAttribute("port", "5900")	/* Temporary */
				// new XAttribute("autoport", "yes")
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
}

public class VirtualMachineVncRenderTarget : IRenderTarget
{
	private int _vmId;
	private byte[]? _framebuffer;
	private readonly object _lock;
	private Size _size;
	private PixelFormat _pixelFormat;
	private Shared.PixelFormat _uniPixelFormat;		/* Universal pixel format */
	private bool _grabbed = false;
	private GCHandle? _framebufferHandle;
	private Action<VirtualMachineFrame> _onNewFrame;

	public VirtualMachineVncRenderTarget(int vmId, PixelFormat pixelFormat, Action<VirtualMachineFrame> onNewFrame)
	{
		_vmId = vmId;
		_pixelFormat = pixelFormat;
		_uniPixelFormat = new Shared.PixelFormat(_pixelFormat.BitsPerPixel, _pixelFormat.HasAlpha, _pixelFormat.RedShift,
			_pixelFormat.GreenShift, _pixelFormat.BlueShift, _pixelFormat.AlphaShift);
		
		_onNewFrame = onNewFrame;
		_lock = new object();
		_size = new Size(0, 0);
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
			if (_framebuffer == null || _size != size)
			{
				_size = size;
				_framebuffer = new byte[_size.Width * _size.Height * _pixelFormat.BytesPerPixel];
			}

			_framebufferHandle = GCHandle.Alloc(_framebuffer, GCHandleType.Pinned);
			return new FramebufferReference(_framebufferHandle.Value.AddrOfPinnedObject(), _size, _pixelFormat)
			{
				Released = OnFramebufferReleased
			};
		}
	}

	private void OnFramebufferReleased(FramebufferReference framebufferReference)
	{
		_onNewFrame.Invoke(new VirtualMachineFrame(_vmId, _uniPixelFormat,
			new System.Drawing.Size(_size.Width, _size.Height), _framebuffer!));
		
		_framebufferHandle!.Value.Free();
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

