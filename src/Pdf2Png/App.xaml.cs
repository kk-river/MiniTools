using KK.Lib.MVVMHelper;
using KK.Lib.MVVMHelper.DI;
using Pdf2Png.UI;

namespace Pdf2Png;

public partial class App : MVVMApplication
{
    public override void ConfigureServices(IContainer container)
    {

    }

    public override Type GetWindowType() => typeof(MainWindow);
    public override Type GetWindowViewModelType() => typeof(MainWindowViewModel);
}
