using System.Text.Json;
using System.Reflection;
using System;
using ArisenKernel.Lifecycle;
using ArisenKernel.Diagnostics;

namespace ArisenKernel.Packages;

public class PackageSubsystem : IEngineSubsystem
{
    private readonly Dictionary<string, ArisenPackageInfo> m_LoadedPackages = new();
    private readonly List<PackageLoadContext> m_LoadContexts = new();
    private readonly IPackageResolver m_Resolver = new DefaultPackageResolver();
    private string m_PackagesRoot = string.Empty;

    public string Name => "PackageSubsystem";
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    public void Initialize()
    {
        KernelLog.Info("[PackageSubsystem] Initializing...");
        
        string baseDir = AppContext.BaseDirectory;
        m_PackagesRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Packages")); // Adjust for Dev/Test environment
        if (!Directory.Exists(m_PackagesRoot))
        {
            m_PackagesRoot = Path.Combine(baseDir, "Packages"); // Fallback for deployment
        }

        // Ensure root exists
        if (!Directory.Exists(m_PackagesRoot)) Directory.CreateDirectory(m_PackagesRoot);
        
        // B2: Use synchronous discovery to avoid sync-over-async deadlock
        DiscoverAndLoadPackages();
    }

    private void DiscoverAndLoadPackages()
    {
        var projectSubsystem = EngineKernel.Instance.GetSubsystem<ProjectSubsystem>();
        var projectPackages = projectSubsystem?.ActiveProject?.Packages ?? new List<PackageRequirement>();
        
        var manifests = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();
        var pendingPackages = new Queue<(string Id, string? Url, string? Version)>();
        var processedIds = new HashSet<string>();

        foreach (var p in projectPackages) pendingPackages.Enqueue((p.Id, p.Url, p.Version));

        while (pendingPackages.Count > 0)
        {
            var (id, url, version) = pendingPackages.Dequeue();
            if (processedIds.Contains(id)) continue;
            processedIds.Add(id);

            try
            {
                string? resolvedPath = null;
                if (!string.IsNullOrEmpty(url))
                {
                    // B2: Use synchronous resolution for local paths; remote still uses async
                    if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        string localPath = Uri.UnescapeDataString(url.Substring(7));
                        if (Directory.Exists(localPath))
                            resolvedPath = Path.GetFullPath(localPath);
                        else if (File.Exists(localPath) && localPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            string extractDir = Path.Combine(m_PackagesRoot, Path.GetFileNameWithoutExtension(localPath));
                            if (!Directory.Exists(extractDir))
                                System.IO.Compression.ZipFile.ExtractToDirectory(localPath, extractDir);
                            resolvedPath = extractDir;
                        }
                    }
                    else
                    {
                        resolvedPath = m_Resolver.ResolveAsync(id, url, m_PackagesRoot).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    // If no URL, check if it's already in the root
                    string potentialPath = Path.Combine(m_PackagesRoot, id);
                    if (Directory.Exists(potentialPath))
                    {
                        resolvedPath = potentialPath;
                    }
                    else
                    {
                        // Search in subdirectories as fallback
                        var subDirs = Directory.GetDirectories(m_PackagesRoot);
                        foreach (var sub in subDirs)
                        {
                            if (Path.GetFileName(sub) == id)
                            {
                                resolvedPath = sub;
                                break;
                            }
                        }
                    }
                }

                if (resolvedPath != null)
                {
                    string manifestPath = Path.Combine(resolvedPath, "package.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = TryReadManifest(manifestPath);
                        if (manifest != null)
                        {
                            // Version Warning
                            if (!string.IsNullOrEmpty(version) && manifest.Version != version)
                            {
                                KernelLog.Info($"[PackageSubsystem] Package '{id}' version mismatch. Project requires {version}, found {manifest.Version}.");
                            }

                            manifests.Add((manifestPath, PackageSource.External, manifest));

                            // Add dependencies
                            if (manifest.Dependencies != null)
                            {
                                foreach (var dep in manifest.Dependencies)
                                {
                                    pendingPackages.Enqueue((dep.Key, null, dep.Value));
                                }
                            }
                        }
                    }
                }
                else
                {
                    KernelLog.Info($"[PackageSubsystem] Could not resolve package '{id}' (URL: {url ?? "None"})");
                }
            }
            catch (Exception e)
            {
                KernelLog.Info($"[PackageSubsystem] Error resolving package '{id}': {e.Message}");
            }
        }

        // Pass 2: Sort and Load
        var sortedManifests = SortManifestsByDependency(manifests);
        foreach (var item in sortedManifests)
        {
            LoadPackage(item.ManifestPath, item.Source, item.Manifest);
        }
    }

    private PackageManifest? TryReadManifest(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PackageManifest>(json, options);
        }
        catch (Exception e)
        {
            KernelLog.Info($"[PackageSubsystem] Failed to read manifest at {path}: {e.Message}");
            return null;
        }
    }

    private List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> SortManifestsByDependency(
        List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> manifests)
    {
        var result = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>(); // B3: Track in-progress visits for cycle detection
        var map = manifests.ToDictionary(x => x.Manifest.Id);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!map.TryGetValue(id, out var item)) return;

            // B3: Detect circular dependencies
            if (visiting.Contains(id))
            {
                KernelLog.Warning($"[PackageSubsystem] Circular dependency detected involving package '{id}'. Skipping.");
                return;
            }

            visiting.Add(id);
            if (item.Manifest.Dependencies != null)
            {
                foreach (var depId in item.Manifest.Dependencies.Keys)
                {
                    Visit(depId);
                }
            }
            visiting.Remove(id);
            visited.Add(id);
            result.Add(item);
        }

        foreach (var m in manifests)
        {
            Visit(m.Manifest.Id);
        }

        return result;
    }

    private void LoadPackage(string manifestPath, PackageSource source, PackageManifest manifest)
    {
        if (m_LoadedPackages.ContainsKey(manifest.Id)) return;

        // Verify Engine Version
        if (!string.IsNullOrEmpty(manifest.EngineVersion))
        {
            if (EngineVersion.TryParse(manifest.EngineVersion, out var required))
            {
                if (EngineVersion.Current < required)
                {
                    KernelLog.Info($"[PackageSubsystem] Package {manifest.Id} requires Engine {required}, but current is {EngineVersion.Current}. Compatibility issues may occur.");
                }
            }
        }

        KernelLog.Info($"[PackageSubsystem] Loading Package: {manifest.Name} ({manifest.Id})");

        try
        {
            string rootPath = Path.GetFullPath(Path.GetDirectoryName(manifestPath)!);
            Assembly? assembly = null;
            string entryAssembly = manifest.Entry?.Assembly ?? string.Empty;
            string entryClass = manifest.Entry?.Class ?? string.Empty;

            if (entryAssembly == "ArisenKernel.dll")
            {
                // B4: Use the kernel assembly correctly
                assembly = typeof(PackageSubsystem).Assembly;
            }
            else if (!string.IsNullOrEmpty(entryAssembly))
            {
                string assemblyPath = Path.Combine(rootPath, entryAssembly);
                if (!File.Exists(assemblyPath))
                {
                    KernelLog.Info($"[PackageSubsystem] Entry assembly not found: {assemblyPath}");
                    return;
                }
                var loadContext = new PackageLoadContext(assemblyPath);
                m_LoadContexts.Add(loadContext);
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            }

            object? entryInstance = null;
            if (assembly != null && !string.IsNullOrEmpty(entryClass))
            {
                var type = assembly.GetType(entryClass);
                if (type != null) entryInstance = Activator.CreateInstance(type);
            }

            var packageInfo = new ArisenPackageInfo
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                RootPath = rootPath,
                Source = source,
                EngineVersion = manifest.EngineVersion,
                Dependencies = manifest.Dependencies ?? new(),
                Assembly = assembly,
                EntryInstance = entryInstance
            };

            m_LoadedPackages[manifest.Id] = packageInfo;
        }
        catch (Exception e)
        {
            KernelLog.Info($"[PackageSubsystem] Error loading package {manifest.Id}: {e.Message}");
        }
    }

    public T? GetPackageEntry<T>(string packageId) where T : class
    {
        if (m_LoadedPackages.TryGetValue(packageId, out var info))
        {
            return info.EntryInstance as T;
        }
        return null;
    }

    public IEnumerable<ArisenPackageInfo> GetAllPackages() => m_LoadedPackages.Values;

    public void Shutdown()
    {
        // B19: Call OnUnload on all package entry instances
        foreach (var pkg in m_LoadedPackages.Values)
        {
            if (pkg.EntryInstance is IPackageEntry entry)
            {
                try
                {
                    entry.OnUnload(EngineKernel.Instance.Services);
                }
                catch (Exception e)
                {
                    KernelLog.Info($"[PackageSubsystem] Error unloading package {pkg.Id}: {e.Message}");
                }
            }
        }

        m_LoadedPackages.Clear();

        foreach (var context in m_LoadContexts)
        {
            context.Unload();
        }
        m_LoadContexts.Clear();
    }

    public void Dispose() => Shutdown();

    private class PackageManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public PackageEntryBlock? Entry { get; set; }
        public string EngineVersion { get; set; } = string.Empty;
        public Dictionary<string, string>? Dependencies { get; set; }
    }

    private class PackageEntryBlock
    {
        public string Assembly { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
    }
}

