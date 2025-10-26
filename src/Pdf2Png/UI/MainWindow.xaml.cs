using System.Windows;
using AdonisUI;
using AdonisUI.Controls;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Runtime.InteropServices;
using WinRT;
using System.Diagnostics;

namespace Pdf2Png.UI;

public partial class MainWindow : AdonisWindow
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow()
    {
        ResourceLocator.SetColorScheme(Application.Current.Resources, ResourceLocator.DarkColorScheme);

        InitializeComponent();
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);

            await ViewModel.LoadPdf(fileNames[0]);
            e.Handled = true;
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.All : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnOpenDirectoryPicker(object sender, RoutedEventArgs e)
    {
        //cf: https://cloud.tencent.com/developer/article/1889343

        FolderPicker picker = new() { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add("*"); //これがないとWinRT内部で死ぬ（なぜ。。。）

        IInitializeWithWindow initializeWithCoreWindow = picker.As<IInitializeWithWindow>();
        initializeWithCoreWindow.Initialize(Process.GetCurrentProcess().MainWindowHandle);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.OutputDir = folder.Path.ToString();
        }
    }

    [ComImport]
    [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithWindow
    {
        void Initialize(IntPtr hwnd);
    }
}
