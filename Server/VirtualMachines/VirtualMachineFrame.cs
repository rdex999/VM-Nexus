using Shared;
using Size = System.Drawing.Size;

namespace Server.VirtualMachines;

public class VirtualMachineFrame
{
	public int VmId { get; }
	public Size Size { get; }
	public byte[] Framebuffer { get; }

	public VirtualMachineFrame(int vmId, Size size, byte[] framebuffer)
	{
		VmId = vmId;
		Size = size;
		Framebuffer = framebuffer;
	}
}