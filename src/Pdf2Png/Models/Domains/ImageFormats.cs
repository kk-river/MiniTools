namespace Pdf2Png.Models.Domains;

public enum ImageFormats
{
    Bitmap,
    Png,
    Jpeg,
}


public static class ImageFormatsExtensions
{
    public static string ToExtension(this ImageFormats format)
    {
        return format switch
        {
            ImageFormats.Bitmap => "bmp",
            ImageFormats.Png => "png",
            ImageFormats.Jpeg => "jpg",
            _ => "img",
        };
    }
}
