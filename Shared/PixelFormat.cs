using Newtonsoft.Json;

namespace Shared;

public class PixelFormat
{
	public PixelFormatType Type { get; }

	[JsonConstructor]
	public PixelFormat(PixelFormatType type)
	{
		Type = type;
	}

	public PixelFormat(int bitsPerPixel, bool hasAlpha, int redShift, int greenShift, int blueShift, int alphaShift)
	{
		Type = PixelFormatTypeFromData(bitsPerPixel, hasAlpha, redShift, greenShift, blueShift, alphaShift);
		if (Type == (PixelFormatType)(-1))
		{
			throw new InvalidOperationException("Unsupported pixel format.");
		}
	}

	private PixelFormatType PixelFormatTypeFromData(int bitsPerPixel, bool hasAlpha, int redShift, int greenShift, int blueShift, int alphaShift)
	{
		if (bitsPerPixel == 32)
		{
			/* Can be BGR32 or RGB32 or BGRA8888 or RGBA8888 */

			if (hasAlpha)
			{
				/* Can only be BGRA8888 or RGBA8888 */

				/* Check if BGRA8888 */
				if (alphaShift == 24 && redShift == 16 && greenShift == 8 &&
				    blueShift == 0)
				{
					return PixelFormatType.Bgra8888;
				}

				/* Check if RGBA8888 */
				if (redShift == 0 && greenShift == 8 && blueShift == 16 && alphaShift == 24)
				{
					return PixelFormatType.Rgba8888;
				}
			}
			else
			{
				/* Can only be BGR32 or RGB32 */

				/* Check if BGR32 */
				if (redShift == 16 && greenShift == 8 && blueShift == 0)
				{
					return PixelFormatType.Bgr32;
				}

				/* Check if RGB32 */
				if (redShift == 0 && greenShift == 8 && blueShift == 16)
				{
					return PixelFormatType.Rgb32;
				}
			}
		}
		else if (bitsPerPixel == 24)
		{
			/* Can only be BGR24 or RGB24 */

			/* Check if BGR24 */
			if (redShift == 16 && greenShift == 8 && blueShift == 0)
			{
				return PixelFormatType.Bgr24;
			}

			/* Check if RGB24 */
			if (redShift == 0 && greenShift == 8 && blueShift == 16)
			{
				return PixelFormatType.Rgb24;
			}
		}
		return (PixelFormatType)(-1);
	}
	
	public enum PixelFormatType
	{
		Bgra8888,
		Rgba8888,
		Bgr32,
		Rgb32,
		Bgr24,
		Rgb24,
	}
}