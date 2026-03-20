using ArisenKernel.Packages;
using ArisenKernel.Services;
using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;

namespace ArisenEngine.Platform;

public class DesktopWindowProvider : IWindowProvider
{
    public nint GetWindowHandle() => IntPtr.Zero;
    public (int Width, int Height) GetWindowSize() => (0, 0);
    public event System.EventHandler<(int Width, int Height)> OnWindowResized;
    public void Close() {}

    public WindowProcessor CreateWindowProcessor(IRenderSurface renderSurface)
    {
        return new ArisenEngine.Platform.Desktop.WindowsProcHandler(renderSurface);
    }
}

public class DesktopPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        registry.RegisterService<IWindowProvider>(new DesktopWindowProvider());
        System.Console.WriteLine("[DesktopPackage] Loaded Desktop Platform Integration");
    }

    public void OnUnload(IServiceRegistry registry)
    {
        System.Console.WriteLine("[DesktopPackage] Unloaded Desktop Platform Integration");
    }
}

