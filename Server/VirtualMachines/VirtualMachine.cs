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
	private Domain _domain = null!;
	
	private int _id;
	private DriveDescriptor[] _drives;
	private SharedDefinitions.CpuArchitecture _cpuArchitecture;
	private SharedDefinitions.BootMode _bootMode;

	public VirtualMachine(DatabaseService databaseService, int id, DriveDescriptor[] drives,
		SharedDefinitions.CpuArchitecture cpuArchitecture, SharedDefinitions.BootMode bootMode)
	{
		_databaseService = databaseService;
		_id = id;
		_drives = drives;
		_cpuArchitecture = cpuArchitecture;
		_bootMode = bootMode;
	}

	public void PowerOn(Domain domain)
	{
		_domain = domain;
	}

	public void PowerOff()
	{
		_domain.Shutdown();
	}

	public XDocument AsXmlDocument()
	{
		string cpuArch = _cpuArchitecture switch
		{
			SharedDefinitions.CpuArchitecture.X86_64 => "x86_64",
			SharedDefinitions.CpuArchitecture.X86 => "x86",
			SharedDefinitions.CpuArchitecture.Arm => "ARM",
			_ => throw new ArgumentOutOfRangeException()
		};
		XElement os = new XElement("os",
			new XElement("type", "hvm", 
				new XAttribute("arch", cpuArch),
				new XAttribute("machine", "pc-q35-10.1")
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
			os.Add(new XElement("smbios", new XAttribute("mode", "sysinfo")));
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
		
		XDocument doc = new XDocument(
			new XElement("domain", new XAttribute("type", "kvm"),
				new XElement("name", _id.ToString()),
				new XElement("memory", "8192", new XAttribute("unit", "MiB")),
				new XElement("features",
					new XElement("vmport", new XAttribute("state", "off")),
					new XElement("acpi"),
					new XElement("apic")
				),
				os
			)
		);
		
		return doc;
	}
	
}
