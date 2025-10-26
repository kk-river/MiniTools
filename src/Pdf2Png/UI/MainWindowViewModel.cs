using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pdf2Png.Models.Domains;
using Pdf2Png.Models.Infra;
using Pdf2Png.Util;
using Reactive.Bindings;
using System.Reactive.Linq;

namespace Pdf2Png.UI;

[INotifyPropertyChanged]
internal partial class MainWindowViewModel
{
    [ObservableProperty]
    private uint _thumbnailWidth = 256;

    [ObservableProperty]
    private uint _thumbnailHeight = 144;

    [ObservableProperty]
    private bool _keepThumbnailAspectRatio = true;

    [ObservableProperty]
    private bool _fillThumbnail = false;

    [NotifyCanExecuteChangedFor(nameof(ExportPagesToFileCommand))]
    [ObservableProperty]
    private string _outputDir = string.Empty;

    [NotifyCanExecuteChangedFor(nameof(ExportPagesToFileCommand))]
    [NotifyPropertyChangedFor(nameof(OutputFilenameSample))]
    [ObservableProperty]
    private string _outputFilename = string.Empty;

    public string OutputFilenameSample => Path.ChangeExtension(OutputFilename.Replace("{Page}", "1"), SelectedFormat.ToExtension());

    [ObservableProperty]
    private uint _outputWidth = 1920;

    [ObservableProperty]
    private uint _outputHeight = 1080;

    [ObservableProperty]
    private bool _keepOutputAspectRatio = true;

    [ObservableProperty]
    private bool _fillOutput = false;

    [ObservableProperty]
    private bool _loading = false;

    [NotifyCanExecuteChangedFor(nameof(SelectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(UnselectAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadPdfCommand))]
    [ObservableProperty]
    private IPdfDocument? _pdfDocument;

    [ObservableProperty]
    private ImageFormats _selectedFormat = ImageFormats.Png;

    [NotifyCanExecuteChangedFor(nameof(ExportPagesToFileCommand))]
    [ObservableProperty]
    private uint _selectedPageCount = 0;

    private bool CanReloadPdf => _pdfDocument is not null;
    private bool CanExportPagesToFile => SelectedPageCount > 0 && !string.IsNullOrWhiteSpace(OutputFilename) && !string.IsNullOrWhiteSpace(OutputDir);

    [ObservableProperty]
    private IList<PageItem> _pages = Array.Empty<PageItem>();

    public Dictionary<ImageFormats, string> FormatOptions { get; } = new() { { ImageFormats.Bitmap, "Bitmap" }, { ImageFormats.Png, "Png" }, { ImageFormats.Jpeg, "Jpeg" } };

    internal async Task LoadPdf(string path)
    {
        //TODO
        if (!File.Exists(path) || Directory.Exists(path)) { return; }

        Loading = true;
        try
        {
            if (string.IsNullOrWhiteSpace(OutputDir)) { OutputDir = Path.GetDirectoryName(path) ?? string.Empty; }
            if (string.IsNullOrWhiteSpace(OutputFilename)) { OutputFilename = $$"""{{Path.GetFileNameWithoutExtension(path)}}_{Page}"""; }

            await Task.Run(() =>
            {
                PdfDocument?.Dispose();
                Pages?.ToList().ForEach(page => page.Dispose());
            });

            PdfDocument = new WinRTPdfDocument(path) { ThumbnailSize = new(ThumbnailWidth, ThumbnailHeight) };
            _ = PdfDocument.Pages.Where(static pages => pages is not null)
                .Subscribe(pages => Pages = pages.Select(page => new PageItem(this, page.PageNo + 1, page.Size, page.Thumbnail)).ToArray());

            try
            {
                await PdfDocument.LoadAsync(KeepThumbnailAspectRatio, FillThumbnail);
            }
            catch (FileFormatException)
            {
                PdfDocument.Dispose();
                PdfDocument = null;
                Pages?.ToList().ForEach(page => page.Dispose());
                Pages = Array.Empty<PageItem>();

                //TODO なんかメッセージ出す
            }
        }
        finally
        {
            Loading = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanReloadPdf))]
    private async Task ReloadPdf() => await LoadPdf(_pdfDocument!.FilePath);

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanExportPagesToFile))]
    private async Task ExportPagesToFile()
    {
        if (!Directory.Exists(OutputDir)) { return; }

        ImageFormats format = SelectedFormat;
        string outputDir = OutputDir;
        string outputFilename = _outputFilename;
        PixelSize outputSize = new(OutputWidth, OutputHeight);

        IList<SavePageInfo> pageList = Pages
            .Where(page => page.IsSelected)
            .Select(page => (page.PageNo, Path: Path.Combine(outputDir, Path.ChangeExtension(OutputFilename.Replace("{Page}", page.PageNo.ToString()), SelectedFormat.ToExtension()))))
            .Select(pair => new SavePageInfo(pair.PageNo - 1, pair.Path, format, outputSize, KeepOutputAspectRatio, FillOutput)).ToArray();

        await PdfDocument!.SavePagesAsync(pageList);
    }

    [RelayCommand(CanExecute = nameof(CanReloadPdf))]
    private void SelectAll() { foreach (PageItem item in Pages) { item.IsSelected = true; } }

    [RelayCommand(CanExecute = nameof(CanReloadPdf))]
    private void UnselectAll() { foreach (PageItem item in Pages) { item.IsSelected = false; } }

    [INotifyPropertyChanged]
    internal partial class PageItem : IDisposable
    {
        public MainWindowViewModel ViewModel { get; private set; }

        [ObservableProperty]
        private bool _isSelected;

        public uint PageNo { get; }

        public PixelSize Size { get; }

        public IReadOnlyReactiveProperty<BitmapSource?> Thumbnail { get; }

        internal PageItem(MainWindowViewModel viewModel, uint pageNo, PixelSize size, IReadOnlyReactiveProperty<BitmapSource?> thumbnail)
        {
            ViewModel = viewModel;
            PageNo = pageNo;
            Thumbnail = thumbnail;
            Size = size;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            ViewModel.SelectedPageCount = value ? ViewModel.SelectedPageCount + 1 : ViewModel.SelectedPageCount - 1;
        }

        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                ViewModel = null!;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
