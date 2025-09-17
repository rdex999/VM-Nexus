using System;
using System.Xml.Linq;
using libvirt;
using Server.Drives;
using Server.Services;
using Shared;

namespace Server.VirtualMachines;

public class VirtualMachine
{
	private readonly DatabaseService _databaseService;
	private readonly DriveService _driveService;
	private Domain _domain = null!;
	
	private int _id;
	private DriveDescriptor[] _drives;
	private SharedDefinitions.CpuArchitecture _cpuArchitecture;
	private SharedDefinitions.BootMode _bootMode;

	public VirtualMachine(DatabaseService databaseService, DriveService driveService, int id, DriveDescriptor[] drives,
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
	{
		_databaseService = databaseService;
		_driveService = driveService;
		_id = id;
		_drives = drives;
		_cpuArchitecture = cpuArchitecture;
		_bootMode = bootMode;
	}

	public ExitCode PowerOn(Connect libvirtConnection)
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

		return ExitCode.Success;
	}

	public void PowerOff()
	{
		_domain.Shutdown();
	}

	private XDocument AsXmlDocument()
	{
		string cpuArch = _cpuArchitecture switch
		{
			SharedDefinitions.CpuArchitecture.X86_64 => "x86_64",
			SharedDefinitions.CpuArchitecture.X86 => "x86_64",
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
			new XElement("input", new XAttribute("type", "keyboard"), new XAttribute("bus", "ps2"))
		);
		
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
	
}
