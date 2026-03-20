using ArisenKernel.Services;

namespace ArisenKernel.Packages;

public interface IPackageEntry
{
    void OnLoad(IServiceRegistry services);
    void OnUnload(IServiceRegistry services);
}
