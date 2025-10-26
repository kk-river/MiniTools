using System.IO;
using System.Reactive.Disposables;
using System.Windows.Media.Imaging;
using Pdf2Png.Models.Domains;
using Pdf2Png.Util;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Pdf2Png.Models.Infra;

internal class WinRTPdfDocument : IPdfDocument
{
    private readonly CompositeDisposable _disposables = new();
    private readonly CancellationTokenSource _thumbnailLoadingCTokenSource;

    private PdfDocument? _document;

    public string Filename { get; }
    public string FilePath { get; }

    private readonly ReactiveProperty<IReadOnlyList<PageInfo>> _pages;
    public IReadOnlyReactiveProperty<IReadOnlyList<IPageInfo>> Pages => _pages;

    private readonly ReactiveProperty<int> _createdThumbnailCount;
    public IReadOnlyReactiveProperty<int> CreatedThumbnailCount => _createdThumbnailCount;

    private readonly ReactiveProperty<bool> _isLoading;
    public IReadOnlyReactiveProperty<bool> IsLoading => _isLoading;

    public PixelSize ThumbnailSize { get; init; } = new(256, 144);

    public WinRTPdfDocument(string path)
    {
        FilePath = path;
        Filename = Path.GetFileName(path);

        _thumbnailLoadingCTokenSource = new CancellationTokenSource().AddTo(_disposables);
        _pages = new ReactiveProperty<IReadOnlyList<PageInfo>>().AddTo(_disposables);
        _createdThumbnailCount = new ReactiveProperty<int>(0).AddTo(_disposables);
        _isLoading = new ReactiveProperty<bool>(false).AddTo(_disposables);
    }

    public async Task LoadAsync(bool keepThumbnailAspectRatio = true, bool fill = false)
    {
        if (!File.Exists(FilePath))
        {
            throw new FileNotFoundException("File not found.", FilePath);
        }

        if (_document is not null)
        {
            throw new InvalidOperationException("Already loaded!");
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(FilePath);

        //LoadFromFileAsyncだとファイルがロックされる（ただこっちの方が遅そう？）
        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
        {
            try
            {
                _document = await PdfDocument.LoadFromStreamAsync(stream);
            }
            catch
            {
                throw new FileFormatException($"Fail to open. FilePath = {FilePath}");
            }
        }

        _pages.Value = Enumerable.Range(0, (int)_document.PageCount)
            .Select(i => _document.GetPage((uint)i))
            .Select(pdfPage => new PageInfo(pdfPage.Index, new PixelSize(pdfPage.Size.Width, pdfPage.Size.Height)).AddTo(_disposables))
            .ToArray();

        //Not waiting
        _ = CreateThumbnailsAsync(_thumbnailLoadingCTokenSource.Token, keepThumbnailAspectRatio, fill);
    }

    private async Task CreateThumbnailsAsync(CancellationToken token, bool keepAspectRatio = true, bool fill = false)
    {
        PdfDocument document = _document ?? throw new InvalidOperationException();
        _isLoading.Value = true;
        try
        {
            IEnumerable<Task> tasks = Enumerable.Range(0, (int)document.PageCount)
                .GroupBy(static pageNo => pageNo % 4)
                .Select(async group =>
                    {
                        foreach (int pageNo in group)
                        {
                            BitmapSource image;
                            using (PdfPage pdfPage = document.GetPage((uint)pageNo))
                            {
                                image = await CreateBitmapAsync(pdfPage, ThumbnailSize, keepAspectRatio, fill);
                            }

                            _pages.Value[pageNo].SetThumbnail(image);

                            lock (_createdThumbnailCount)
                            {
                                _createdThumbnailCount.Value++;
                            }
                        }
                    });
            await Task.WhenAll(tasks);
        }
        finally
        {
            _isLoading.Value = false;
        }
    }

    public async Task SavePagesAsync(IList<SavePageInfo> pageList)
    {
        PdfDocument document = _document ?? throw new InvalidOperationException();

        IEnumerable<Task> tasks = pageList
            .GroupBy(static page => page.PageNo / 4)
            .Select(async group =>
            {
                foreach (SavePageInfo page in group)
                {
                    //EncoderのSaveが一回しか呼べないから毎回newする
                    BitmapEncoder encoder = page.ImageFormat switch
                    {
                        ImageFormats.Bitmap => new BmpBitmapEncoder(),
                        ImageFormats.Png => new PngBitmapEncoder(),
                        ImageFormats.Jpeg => new JpegBitmapEncoder() { QualityLevel = 100, },
                        _ => throw new NotImplementedException($"{page.ImageFormat} is unsupported."),
                    };

                    BitmapSource image;
                    using (PdfPage pdfPage = document.GetPage(page.PageNo))
                    {
                        image = await CreateBitmapAsync(pdfPage, page.RequestSize, page.KeepAspectRatio, page.Fill);
                    }

                    encoder.Frames.Add(BitmapFrame.Create(image));
                    using (FileStream fileStream = new(page.OutputPath, FileMode.Create, FileAccess.Write))
                    {
                        encoder.Save(fileStream);
                    }
                }
            });
        await Task.WhenAll(tasks);
    }

    private static async Task<BitmapSource> CreateBitmapAsync(PdfPage pdfPage, PixelSize requestSize, bool keepAspectRatio, bool fill)
    {
        using (InMemoryRandomAccessStream stream = new())
        {
            //角度はRender時もSizeプロパティの場合も考慮される。DimentionのMediaBoxを直で見たりする場合は角度の考慮が必要
            PixelSize size =
                requestSize == default ? new(pdfPage.Size.Width, pdfPage.Size.Height)
                : keepAspectRatio ? GetUniformSize(pdfPage.Size.Width, pdfPage.Size.Height, requestSize, fill)
                : requestSize;
            await pdfPage.RenderToStreamAsync(stream, new PdfPageRenderOptions() { DestinationWidth = size.Width, DestinationHeight = size.Height, });

            PngBitmapDecoder decoder = new(stream.AsStream(), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames[0];
        }
    }

    private static PixelSize GetUniformSize(double origWidth, double origHeight, PixelSize requestSize, bool fill)
    {
        bool tooLongVertical = origWidth * requestSize.Height > origHeight * requestSize.Width;
        (uint width, uint height) = (fill, tooLongVertical) switch
        {
            (true, false) or (false, true) => (requestSize.Width, Utils.Round2UInt(origHeight * requestSize.Width / origWidth)), //(Fill, 横が長い) or (NotFill, 縦が長い) => 縦を基準に拡大縮小
            (true, true) or (false, false) => (Utils.Round2UInt(origWidth * requestSize.Height / origHeight), requestSize.Height), //(Fill, 縦が長い) or (NotFill, 横が長い) => 横を基準に拡大縮小
        };

        return new PixelSize(width, height);
    }

    #region Dispose
    private bool _disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _thumbnailLoadingCTokenSource?.Cancel();
                _thumbnailLoadingCTokenSource?.Dispose();

                _disposables.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion Dispose

    private class PageInfo : IPageInfo, IDisposable
    {
        public uint PageNo { get; init; }
        public PixelSize Size { get; init; }

        private readonly IReactiveProperty<BitmapSource?> _thumbnail = new ReactiveProperty<BitmapSource?>();

        public IReadOnlyReactiveProperty<BitmapSource?> Thumbnail => _thumbnail;

        public PageInfo(uint pageNo, PixelSize size)
        {
            PageNo = pageNo;
            Size = size;
        }

        public void SetThumbnail(BitmapSource image) => _thumbnail.Value = image;

        #region Dispose
        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _thumbnail.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion Dispose
    }
}
