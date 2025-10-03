using Shared;
using Size = System.Drawing.Size;

namespace Server.VirtualMachines;

public class VirtualMachineFrame
{
	public int VmId { get; }
	public Size Size { get; }
	public byte[] CompressedFramebuffer { get; }

	public VirtualMachineFrame(int vmId, Size size, byte[] compressedFramebuffer)
	{
		VmId = vmId;
		Size = size;
		CompressedFramebuffer = compressedFramebuffer;
	}
}