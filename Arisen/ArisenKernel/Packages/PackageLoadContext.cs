using System.Runtime.Loader;
using System.Reflection;

namespace ArisenKernel.Packages;

/// <summary>
/// Collectible assembly load context for package-local managed assemblies.
/// Shared assemblies that are already deployed beside the host executable stay in the default context;
/// this context is reserved for package-private assemblies so package entries can be unloaded after shutdown
/// once no runtime references remain.
/// </summary>
public sealed class PackageLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver m_Resolver;

    public PackageLoadContext(string mainAssemblyPath) : base(isCollectible: true)
    {
        m_Resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? assemblyPath = m_Resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = m_Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

