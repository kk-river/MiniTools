using System.Windows;

namespace Pdf2Png;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = new UI.MainWindow()
        {
            DataContext = new UI.MainWindowViewModel(),
        };
        MainWindow.Show();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
    }
}
