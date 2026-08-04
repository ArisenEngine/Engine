using ArisenKernel.Services;

namespace ArisenKernel.Packages;

public interface IPackageEntry
{
    /// <summary>
    /// Reports whether the entry still owns state that requires another <see cref="OnUnload"/>
    /// call. The default preserves one-shot teardown for existing package entries.
    /// </summary>
    bool HasPendingOwnership => false;

    void OnLoad(IServiceRegistry services);
    void OnUnload(IServiceRegistry services);
}
