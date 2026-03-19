using System.Runtime.Loader;
using System.Reflection;

namespace ArisenKernel.Packages;

/// <summary>
/// Provides an isolated loading context for Arisen Packages and DLCs.
/// </summary>
public class PackageLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver m_Resolver;

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

