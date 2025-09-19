using Avalonia.Platform;
using Size = System.Drawing.Size;

namespace Server.VirtualMachines;

public class VirtualMachineFrame
{
	public int VmId { get; }
	public PixelFormat PixelFormat { get; }
	public Size Size { get; }
	public byte[] Framebuffer { get; }

	public VirtualMachineFrame(int vmId, PixelFormat pixelFormat, Size size, byte[] framebuffer)
	{
		VmId = vmId;
		PixelFormat = pixelFormat;
		Size = size;
		Framebuffer = framebuffer;
	}
}