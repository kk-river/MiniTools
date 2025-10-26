using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NugetLicenseRetriever.UI;

namespace NugetLicenseRetriever;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new() { Args = args, DisableDefaults = true, ApplicationName = "NugetLicenseRetriever" });
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();

        IHost host = builder.Build();

        App app = host.Services.GetRequiredService<App>();
        MainWindow window = host.Services.GetRequiredService<MainWindow>();

        host.Start();
        app.Run(window);

        host.StopAsync().GetAwaiter().GetResult();
    }
}
