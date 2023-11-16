using Android.App;
using Android.Content.PM;

using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using System;
using System.Threading;

namespace GameApplicationShell.Android;

[Activity(
    Label = "GameApplicationShell.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        Thread.CurrentThread.Name = "MainThread";
        NebulaEngine.GameApplication.platform = NebulaEngine.RuntimePlatform.Android;
        NebulaEngine.GameApplication.startupPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        NebulaEngine.GameApplication.isInEditor = false;

        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI();
    }
}
