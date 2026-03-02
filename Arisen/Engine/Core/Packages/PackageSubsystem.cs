using System.Text.Json;
using System.Reflection;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Core.Packages;

public class PackageSubsystem : IEngineSubsystem
{
    private readonly Dictionary<string, ArisenPackageInfo> m_LoadedPackages = new();
    private readonly List<PackageLoadContext> m_LoadContexts = new();
    private readonly Dictionary<PackageSource, List<string>> m_CategorySearchPaths = new()
    {
        { PackageSource.Builtin, new List<string>() },
        { PackageSource.UserProject, new List<string>() },
        { PackageSource.External, new List<string>() }
    };

    public string Name => "PackageSubsystem";
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    public void Initialize()
    {
        Logger.Log("[PackageSubsystem] Initializing...");
        
        string baseDir = AppContext.BaseDirectory;
        
        // Simplified: Scanning organized Packages directory in the output
        string packagesRoot = Path.Combine(baseDir, "Packages");
        m_CategorySearchPaths[PackageSource.Builtin].Add(Path.Combine(packagesRoot, "Builtin"));
        m_CategorySearchPaths[PackageSource.UserProject].Add(Path.Combine(packagesRoot, "UserProject"));
        m_CategorySearchPaths[PackageSource.External].Add(Path.Combine(packagesRoot, "External"));
        m_CategorySearchPaths[PackageSource.External].Add(Path.Combine(baseDir, "DLC"));

        DiscoverAndLoadPackages();
    }

    private void DiscoverAndLoadPackages()
    {
        var manifests = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();

        // Pass 1: Scan all manifests
        foreach (var category in m_CategorySearchPaths)
        {
            var source = category.Key;
            foreach (var path in category.Value)
            {
                if (!Directory.Exists(path)) continue;

                var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                foreach (var dir in directories)
                {
                    string manifestPath = Path.Combine(dir, "package.json");
                    if (File.Exists(manifestPath))
                    {
                        var manifest = TryReadManifest(manifestPath);
                        if (manifest != null)
                        {
                            manifests.Add((manifestPath, source, manifest));
                        }
                    }
                }
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
            Logger.Error($"[PackageSubsystem] Failed to read manifest at {path}: {e.Message}");
            return null;
        }
    }

    private List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> SortManifestsByDependency(
        List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> manifests)
    {
        // Simple topological sort
        var result = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();
        var visited = new HashSet<string>();
        var map = manifests.ToDictionary(x => x.Manifest.Id);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!map.TryGetValue(id, out var item)) return;

            visited.Add(id);
            if (item.Manifest.Dependencies != null)
            {
                foreach (var depId in item.Manifest.Dependencies.Keys)
                {
                    Visit(depId);
                }
            }
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
            if (EngineVersion.TryParse(manifest.EngineVersion, out var required) &&
                EngineVersion.Current < required)
            {
                Logger.Warning($"[PackageSubsystem] Package {manifest.Id} requires Engine {required}, but current is {EngineVersion.Current}. Skipping.");
                return;
            }
        }

        Logger.Log($"[PackageSubsystem] Loading Package: {manifest.Name} ({manifest.Id})");

        try
        {
            string rootPath = Path.GetDirectoryName(manifestPath)!;
            Assembly? assembly;
            if (manifest.EntryAssembly == "ArisenEngine.dll")
            {
                assembly = Assembly.GetExecutingAssembly();
            }
            else
            {
                string assemblyPath = Path.Combine(rootPath, manifest.EntryAssembly);
                var loadContext = new PackageLoadContext(assemblyPath);
                m_LoadContexts.Add(loadContext);
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            }

            if (assembly == null) return;

            object? entryInstance = null;
            if (!string.IsNullOrEmpty(manifest.EntryClass))
            {
                var type = assembly.GetType(manifest.EntryClass);
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
            Logger.Error($"[PackageSubsystem] Error loading package {manifest.Id}: {e.Message}");
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
        m_LoadedPackages.Clear();

        // Unload collectible AssemblyLoadContexts to allow GC of loaded assemblies
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
        public string EntryAssembly { get; set; } = string.Empty;
        public string EntryClass { get; set; } = string.Empty;
        public string EngineVersion { get; set; } = string.Empty;
        public Dictionary<string, string>? Dependencies { get; set; }
    }
}
