using ArisenKernel.Packages;
using ArisenKernel.Services;

namespace ArisenEngine.Core;

public class CorePackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        System.Console.WriteLine("[CorePackage] Loaded: Arisen Core Engine Foundation");
    }

    public void OnUnload(IServiceRegistry registry)
    {
    }
}
