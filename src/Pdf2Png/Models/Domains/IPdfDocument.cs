using System.Windows.Media.Imaging;
using Pdf2Png.Util;
using Reactive.Bindings;

namespace Pdf2Png.Models.Domains;

internal interface IPdfDocument : IDisposable
{
    string Filename { get; }
    string FilePath { get; }

    IReadOnlyReactiveProperty<IReadOnlyList<IPageInfo>> Pages { get; }
    IReadOnlyReactiveProperty<int> CreatedThumbnailCount { get; }
    IReadOnlyReactiveProperty<bool> IsLoading { get; }

    PixelSize ThumbnailSize { get; init; }

    Task LoadAsync(bool keepThumbnailAspectRatio = true, bool fill = false);
    Task SavePagesAsync(IList<SavePageInfo> pageList);
}

internal partial interface IPageInfo
{
    uint PageNo { get; }
    PixelSize Size { get; }

    public IReadOnlyReactiveProperty<BitmapSource?> Thumbnail { get; }
}

internal record struct SavePageInfo(uint PageNo, string OutputPath, ImageFormats ImageFormat, PixelSize RequestSize, bool KeepAspectRatio, bool Fill);
