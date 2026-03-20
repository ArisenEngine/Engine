using ArisenKernel.Packages;
using ArisenKernel.Services;

namespace ArisenEngine.Resources;

public class ResourcesPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        System.Console.WriteLine("[ResourcesPackage] Loaded: Arisen Asset Resources");
    }

    public void OnUnload(IServiceRegistry registry)
    {
    }
}
