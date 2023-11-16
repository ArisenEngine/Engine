using System;
using System.Threading;
using UIKit;

namespace GameApplicationShell.iOS;

public class Application
{
    // This is the main entry point of the application.
    static void Main(string[] args)
    {
        Thread.CurrentThread.Name = "MainThread";
        NebulaEngine.GameApplication.platform = NebulaEngine.RuntimePlatform.IOS;
        NebulaEngine.GameApplication.startupPath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        NebulaEngine.GameApplication.isInEditor = false;

        // if you want to use a different Application Delegate class from "AppDelegate"
        // you can specify it here.
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
