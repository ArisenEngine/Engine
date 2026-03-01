using System.Text.Json;
using System.Reflection;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Core.Packages;

public class PackageSubsystem : IEngineSubsystem
{
    private readonly Dictionary<string, ArisenPackageInfo> m_LoadedPackages = new();
    private readonly Dictionary<PackageSource, List<string>> m_CategorySearchPaths = new()
    {
        { PackageSource.Builtin, new List<string>() },
        { PackageSource.UserProject, new List<string>() },
        { PackageSource.External, new List<string>() }
    };

    public string Name => "PackageSubsystem";
    public EnginePhase InitPhase => EnginePhase.Setup;

    public void Initialize()
    {
        Logger.Log("[PackageSubsystem] Initializing...");
        
        string baseDir = AppContext.BaseDirectory;
        
        // 1. Builtin Packages (typically inside the Engine folder)
        m_CategorySearchPaths[PackageSource.Builtin].Add(Path.Combine(baseDir, "Engine/Packages"));
        
        // 2. User Project Packages
        m_CategorySearchPaths[PackageSource.UserProject].Add(Path.Combine(baseDir, "Packages"));
        
        // 3. External / DLC / Workshop
        m_CategorySearchPaths[PackageSource.External].Add(Path.Combine(baseDir, "DLC"));
        m_CategorySearchPaths[PackageSource.External].Add(Path.Combine(baseDir, "Workshop"));

        DiscoverAndLoadPackages();
    }

    private void DiscoverAndLoadPackages()
    {
        foreach (var category in m_CategorySearchPaths)
        {
            var source = category.Key;
            foreach (var path in category.Value)
            {
                if (!Directory.Exists(path)) continue;

                Logger.Log($"[PackageSubsystem] Scanning {source}: {path}");
                var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                foreach (var dir in directories)
                {
                    string manifestPath = Path.Combine(dir, "package.json");
                    if (File.Exists(manifestPath))
                    {
                        LoadPackageFromManifest(manifestPath, source);
                    }
                }
            }
        }
    }

    private void LoadPackageFromManifest(string manifestPath, PackageSource source)
    {
        try
        {
            string json = File.ReadAllText(manifestPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var manifest = JsonSerializer.Deserialize<PackageManifest>(json, options);

            if (manifest == null || string.IsNullOrEmpty(manifest.Id)) return;

            Logger.Log($"[PackageSubsystem] Loading Package: {manifest.Name} ({manifest.Id})");

            string rootPath = Path.GetDirectoryName(manifestPath)!;
            
            // For now, if it's "ArisenEngine.dll", we assume it's already loaded or in the same folder.
            // In a real DLC scenario, we would use PackageLoadContext to load a specific separate DLL.
            Assembly? assembly;
            if (manifest.EntryAssembly == "ArisenEngine.dll")
            {
                assembly = Assembly.GetExecutingAssembly();
            }
            else
            {
                string assemblyPath = Path.Combine(rootPath, manifest.EntryAssembly);
                var loadContext = new PackageLoadContext(assemblyPath);
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            }

            if (assembly == null) return;

            // Instantiate the entry class if specified
            object? entryInstance = null;
            if (!string.IsNullOrEmpty(manifest.EntryClass))
            {
                var type = assembly.GetType(manifest.EntryClass);
                if (type != null)
                {
                    entryInstance = Activator.CreateInstance(type);
                }
            }

            var packageInfo = new ArisenPackageInfo
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                RootPath = rootPath,
                Source = source,
                Assembly = assembly,
                EntryInstance = entryInstance
            };

            m_LoadedPackages[manifest.Id] = packageInfo;
        }
        catch (Exception e)
        {
            Logger.Error($"[PackageSubsystem] Failed to load package at {manifestPath}: {e.Message}");
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
    }

    public void Dispose() => Shutdown();

    // Internal class for manifest deserialization
    private class PackageManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string EntryAssembly { get; set; } = string.Empty;
        public string EntryClass { get; set; } = string.Empty;
    }
}
