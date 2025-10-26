namespace Pdf2Png.Util;

public record struct PixelSize(uint Width, uint Height)
{
    public PixelSize(double width, double height) : this(Utils.Round2UInt(width), Utils.Round2UInt(height)) { }
}
