using System.Threading.Tasks;

namespace ArisenKernel.Packages;

/// <summary>
/// Interface for resolving and downloading packages from various sources (Local, Git, Zip).
/// </summary>
public interface IPackageResolver
{
    /// <summary>
    /// Checks if this resolver can handle the given URL.
    /// </summary>
    bool CanResolve(string url);

    /// <summary>
    /// Resolves the package from the URL to a local destination directory.
    /// </summary>
    Task<string> ResolveAsync(string id, string url, string destinationDir);
}

