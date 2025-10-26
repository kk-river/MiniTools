using KK.Lib.Toolkit;

namespace Pdf2Png.Models.Domains;

public enum ImageFormats
{
    [Extension("bmp")]
    Bitmap,
    [Extension("png")]
    Png,
    [Extension("jpg")]
    Jpeg,
}


public class ExtensionAttribute : AttachedValueAttribute<string>
{
    public ExtensionAttribute(string value) : base(value) { }
}
