namespace GameTest;
using NebulaEngine;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        var host = new WindowHost();
        host.Name = "Nebula";
        host.Width = 1280;
        host.Height = 720;
       
        
        GameApplication.platform = RuntimePlatform.Windows;
        var context = new NebulaEngine.RenderContext(host.Handle, host.Width, host.Height);
        
        host.ResizeEnd += (sender, args) =>
        {
            context.Resize();
        };

        var thread = new Thread(() =>
        {
            Engine.Run(Update);
            Engine.Dispose();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        
        host.Closed += (sender, args) =>
        {
            Application.Exit();
        };
        
        Application.Run(host);
    }

    static void Update()
    {
        Console.WriteLine(DateTime.Now.ToUniversalTime());
    }
}