using ArisenKernel.Packages;
using ArisenKernel.Services;

namespace ArisenEngine.Rendering;

public class RenderingPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        System.Console.WriteLine("[RenderingPackage] Loaded: Arisen Render Pipeline");
    }

    public void OnUnload(IServiceRegistry registry)
    {
    }
}
