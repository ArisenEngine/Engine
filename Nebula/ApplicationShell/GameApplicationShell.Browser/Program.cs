using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;

using GameApplicationShell;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        NebulaEngine.GameApplication.platform = NebulaEngine.RuntimePlatform.Browser;
        NebulaEngine.GameApplication.startupPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        NebulaEngine.GameApplication.isInEditor = false;

        await BuildAvaloniaApp()
            .WithInterFont()
            .UseReactiveUI()
            .StartBrowserAppAsync("out");

    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
