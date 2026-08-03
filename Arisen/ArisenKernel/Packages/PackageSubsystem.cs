using System.Text.Json;
using System.Reflection;
using System;
using System.Runtime.InteropServices;
using ArisenKernel.Lifecycle;
using ArisenKernel.Diagnostics;
using ArisenKernel.Services;
using Arisen.Versioning;

namespace ArisenKernel.Packages;

internal interface INativePackageRuntimeApi
{
    IntPtr Load(string packageId, string runtimePath);
    int InvokeLifecycle(
        string packageId,
        IntPtr libraryHandle,
        string runtimePath,
        string exportName,
        string phase);
    void Free(IntPtr libraryHandle);
}

public class PackageSubsystem : IEngineSubsystem
{
    private readonly Dictionary<string, ArisenPackageInfo> m_LoadedPackages = new();
    private readonly List<string> m_LoadOrder = new();
    private readonly Dictionary<string, PackageLoadContext> m_LoadContexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<LoadedNativeRuntime>> m_LoadedNativeRuntimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPackageResolver m_Resolver = new DefaultPackageResolver();
    private readonly INativePackageRuntimeApi m_NativeRuntimeApi;
    private string m_PackagesRoot = string.Empty;
    private AggregateException? m_ShutdownFailure;

    public PackageSubsystem()
        : this(new NativePackageRuntimeApi())
    {
    }

    internal PackageSubsystem(INativePackageRuntimeApi nativeRuntimeApi)
    {
        m_NativeRuntimeApi = nativeRuntimeApi ?? throw new ArgumentNullException(nameof(nativeRuntimeApi));
    }

    public string Name => "PackageSubsystem";
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;
    internal int LoadedContextCount => m_LoadContexts.Count;
    internal int LoadedNativeRuntimeCount => m_LoadedNativeRuntimes.Values.Sum(runtimes => runtimes.Count);

    public void MountPackages(
        IEnumerable<string> packageUrls,
        IEnumerable<SelectedPackageRequirement>? selectedRequirements = null)
    {
        if (packageUrls == null) throw new ArgumentNullException(nameof(packageUrls));
        int initialPackageCount = m_LoadOrder.Count;

        try
        {
            var manifests = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();
            foreach (var packageUrl in packageUrls)
            {
                if (string.IsNullOrWhiteSpace(packageUrl)) continue;

                string packageJsonPath = Path.Combine(packageUrl, "package.json");
                if (!File.Exists(packageJsonPath))
                {
                    throw new FileNotFoundException($"Package manifest not found for '{packageUrl}'.", packageJsonPath);
                }

                var manifest = TryReadManifest(packageJsonPath);
                if (manifest == null)
                {
                    throw new InvalidOperationException($"Failed to read package manifest '{packageJsonPath}'.");
                }

                manifests.Add((packageJsonPath, PackageSource.External, manifest));
            }

            ValidateManifestGraph(
                manifests,
                selectedRequirements ?? Array.Empty<SelectedPackageRequirement>());
            foreach (var package in SortManifestsByDependency(manifests))
            {
                LoadPackage(package.ManifestPath, package.Source, package.Manifest);
            }

            ValidateRequiredServices();
        }
        catch (Exception mountError)
        {
            var rollbackErrors = new List<Exception>();
            RollbackLoadedPackagesTo(initialPackageCount, rollbackErrors);
            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, mountError);
                throw new AggregateException(
                    "Package graph mount failed and rollback reported additional errors.",
                    rollbackErrors);
            }

            throw;
        }
    }

    public void Initialize()
    {
        KernelLog.Info("[PackageSubsystem] Initializing...");

        // If packages were already registered by the Bootstrapper during early mount,
        // we skip the disk discovery phase to avoid redundancy.
        if (m_LoadedPackages.Count > 0)
        {
            KernelLog.Info($"[PackageSubsystem] {m_LoadedPackages.Count} packages already registered by Bootstrapper. Skipping discovery.");
            return;
        }
        
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
        ValidateManifestGraph(
            manifests,
            projectPackages.Select(package => new SelectedPackageRequirement(
                package.Id,
                package.Version ?? string.Empty,
                "workspace manifest")));
        var sortedManifests = SortManifestsByDependency(manifests);
        int initialPackageCount = m_LoadOrder.Count;
        try
        {
            foreach (var item in sortedManifests)
            {
                LoadPackage(item.ManifestPath, item.Source, item.Manifest);
            }

            ValidateRequiredServices();
        }
        catch (Exception mountError)
        {
            var rollbackErrors = new List<Exception>();
            RollbackLoadedPackagesTo(initialPackageCount, rollbackErrors);
            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, mountError);
                throw new AggregateException(
                    "Discovered package graph mount failed and rollback reported additional errors.",
                    rollbackErrors);
            }

            throw;
        }
    }

        private PackageManifest? TryReadManifest(string path)
    {
        try
        {
            var manifest = ManifestJson.DeserializeFile<PackageManifest>(path);
            if (manifest == null) return null;

            string generatedPath = Path.Combine(Path.GetDirectoryName(path)!, "package.generated.json");
            if (File.Exists(generatedPath))
            {
                var generatedManifest = ManifestJson.DeserializeFile<PackageManifest>(generatedPath);
                if (generatedManifest != null)
                {
                    MergeGeneratedManifest(manifest, generatedManifest);
                }
            }

            return manifest;
        }
        catch (Exception e)
        {
            KernelLog.Info($"[PackageSubsystem] Failed to read manifest at {path}: {e.Message}");
            return null;
        }
    }

    private static void MergeGeneratedManifest(PackageManifest manifest, PackageManifest generated)
    {
        if (generated.Entry != null) manifest.Entry = generated.Entry;

        if (generated.Subsystems != null && generated.Subsystems.Count > 0)
        {
            manifest.Subsystems = MergeSubsystems(manifest.Subsystems, generated.Subsystems);
        }

        if (generated.Services != null)
        {
            manifest.Services ??= new PackageServicesBlock();
            if (generated.Services.Provides != null && generated.Services.Provides.Count > 0)
            {
                manifest.Services.Provides = MergeJsonElements(manifest.Services.Provides, generated.Services.Provides);
            }

            if (generated.Services.Requires != null && generated.Services.Requires.Count > 0)
            {
                manifest.Services.Requires = MergeJsonElements(manifest.Services.Requires, generated.Services.Requires);
            }
        }
    }

    private static List<PackageSubsystemBlock> MergeSubsystems(List<PackageSubsystemBlock>? authored, List<PackageSubsystemBlock> generated)
    {
        var result = new List<PackageSubsystemBlock>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var subsystem in authored ?? Enumerable.Empty<PackageSubsystemBlock>())
        {
            if (seen.Add(subsystem.Class)) result.Add(subsystem);
        }

        foreach (var subsystem in generated)
        {
            int existingIndex = result.FindIndex(x => string.Equals(x.Class, subsystem.Class, StringComparison.Ordinal));
            if (existingIndex >= 0) result[existingIndex] = subsystem;
            else result.Add(subsystem);
        }

        return result;
    }

    private static List<JsonElement> MergeJsonElements(List<JsonElement>? authored, List<JsonElement> generated)
    {
        var result = new List<JsonElement>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in authored ?? Enumerable.Empty<JsonElement>())
        {
            string key = GetServiceKey(element);
            if (seen.Add(key)) result.Add(element.Clone());
        }

        foreach (var element in generated)
        {
            string key = GetServiceKey(element);
            int existingIndex = result.FindIndex(x => string.Equals(GetServiceKey(x), key, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0) result[existingIndex] = element.Clone();
            else result.Add(element.Clone());
        }

        return result;
    }

    private static string GetServiceKey(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("interface", out var interfaceElement)
            && interfaceElement.ValueKind == JsonValueKind.String)
        {
            return interfaceElement.GetString() ?? element.GetRawText();
        }

        return element.GetRawText();
    }

    private List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> SortManifestsByDependency(
        List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> manifests)
    {
        var result = new List<(string ManifestPath, PackageSource Source, PackageManifest Manifest)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitPath = new List<string>();
        var map = manifests.ToDictionary(
            item => item.Manifest.Id,
            StringComparer.OrdinalIgnoreCase);

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!map.TryGetValue(id, out var item)) return;

            // B3: Detect circular dependencies
            if (visiting.Contains(id))
            {
                int cycleStart = visitPath.FindIndex(value =>
                    string.Equals(value, id, StringComparison.OrdinalIgnoreCase));
                IEnumerable<string> cycle = cycleStart >= 0
                    ? visitPath.Skip(cycleStart).Append(id)
                    : visitPath.Append(id);
                throw new InvalidDataException(
                    $"Package dependency cycle detected: {string.Join(" -> ", cycle)}.");
            }

            visiting.Add(id);
            visitPath.Add(id);
            if (item.Manifest.Dependencies != null)
            {
                foreach (var depId in item.Manifest.Dependencies.Keys)
                {
                    Visit(depId);
                }
            }
            visitPath.RemoveAt(visitPath.Count - 1);
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

    private void ValidateManifestGraph(
        IReadOnlyCollection<(string ManifestPath, PackageSource Source, PackageManifest Manifest)> manifests,
        IEnumerable<SelectedPackageRequirement> selectedRequirements)
    {
        var errors = new List<string>();
        var versions = new Dictionary<string, SemanticVersion>(StringComparer.OrdinalIgnoreCase);
        foreach (ArisenPackageInfo loadedPackage in m_LoadedPackages.Values)
        {
            if (SemanticVersion.TryParseExact(loadedPackage.Version, out SemanticVersion loadedVersion))
            {
                versions[loadedPackage.Id] = loadedVersion;
            }
        }

        var selectedManifests = new Dictionary<string, PackageManifest>(StringComparer.OrdinalIgnoreCase);
        foreach ((string manifestPath, _, PackageManifest manifest) in manifests)
        {
            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                errors.Add($"Package manifest '{manifestPath}' has an empty id.");
                continue;
            }

            if (!selectedManifests.TryAdd(manifest.Id, manifest))
            {
                errors.Add($"Package graph contains duplicate package id '{manifest.Id}'.");
                continue;
            }

            if (!SemanticVersion.TryParseExact(manifest.Version, out SemanticVersion packageVersion))
            {
                errors.Add(
                    $"Package '{manifest.Id}' declares invalid semantic version '{manifest.Version}'. Expected major.minor.patch.");
            }
            else
            {
                versions[manifest.Id] = packageVersion;
            }

            try
            {
                ValidatePackageEngineCompatibility(manifest);
            }
            catch (InvalidDataException exception)
            {
                errors.Add(exception.Message);
            }
        }

        foreach (SelectedPackageRequirement requirement in selectedRequirements)
        {
            ValidateVersionRequirement(requirement, versions, errors);
        }

        foreach ((string packageId, PackageManifest manifest) in selectedManifests)
        {
            foreach ((string dependencyId, string versionExpression) in
                     manifest.Dependencies ?? new Dictionary<string, string>())
            {
                ValidateVersionRequirement(
                    new SelectedPackageRequirement(
                        dependencyId,
                        versionExpression,
                        $"package dependency edge '{packageId} -> {dependencyId}'"),
                    versions,
                    errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                "Package compatibility preflight failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => $"- {error}")));
        }
    }

    private static void ValidateVersionRequirement(
        SelectedPackageRequirement requirement,
        IReadOnlyDictionary<string, SemanticVersion> versions,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(requirement.PackageId))
        {
            errors.Add($"{requirement.Source} contains an empty package id.");
            return;
        }

        if (!versions.TryGetValue(requirement.PackageId, out SemanticVersion resolvedVersion))
        {
            errors.Add(
                $"{requirement.Source} requires package '{requirement.PackageId}', but it is not selected.");
            return;
        }

        if (string.IsNullOrWhiteSpace(requirement.VersionExpression))
        {
            errors.Add(
                $"{requirement.Source} requires package '{requirement.PackageId}' without a version constraint.");
            return;
        }

        if (!SemanticVersionRange.TryParse(
                requirement.VersionExpression,
                out SemanticVersionRange range,
                out string rangeError))
        {
            errors.Add(
                $"{requirement.Source} declares invalid constraint '{requirement.VersionExpression}' for package '{requirement.PackageId}': {rangeError}");
            return;
        }

        if (!range.Matches(resolvedVersion))
        {
            errors.Add(
                $"{requirement.Source} requires package '{requirement.PackageId}' at '{requirement.VersionExpression}', " +
                $"but the selected version is '{resolvedVersion}'.");
        }
    }

    private static string ValidatePackageEngineCompatibility(PackageManifest manifest)
    {
        string? canonicalMinimum = manifest.Engine?.MinVersion;
        string? legacyMinimum = manifest.EngineVersion;
        if (manifest.Engine != null && string.IsNullOrWhiteSpace(canonicalMinimum))
        {
            throw new InvalidDataException(
                $"Package '{manifest.Id}' declares engine compatibility without engine.minVersion.");
        }

        if (legacyMinimum != null && string.IsNullOrWhiteSpace(legacyMinimum))
        {
            throw new InvalidDataException(
                $"Package '{manifest.Id}' declares legacy engineVersion with an empty value.");
        }

        if (!string.IsNullOrWhiteSpace(canonicalMinimum) &&
            !string.IsNullOrWhiteSpace(legacyMinimum) &&
            !string.Equals(canonicalMinimum, legacyMinimum, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Package '{manifest.Id}' declares conflicting engine.minVersion '{canonicalMinimum}' and legacy engineVersion '{legacyMinimum}'.");
        }

        string effectiveMinimum = !string.IsNullOrWhiteSpace(canonicalMinimum)
            ? canonicalMinimum
            : legacyMinimum ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(legacyMinimum))
        {
            KernelLog.Warning(
                $"[PackageSubsystem] Package '{manifest.Id}' uses legacy engineVersion. Migrate to engine.minVersion.");
        }

        if (string.IsNullOrWhiteSpace(effectiveMinimum)) return string.Empty;

        if (!SemanticVersion.TryParseExact(effectiveMinimum, out SemanticVersion minimumVersion))
        {
            throw new InvalidDataException(
                $"Package '{manifest.Id}' declares invalid engine.minVersion '{effectiveMinimum}'. Expected major.minor.patch.");
        }

        if (EngineCompatibility.CurrentVersion.CompareTo(minimumVersion) < 0)
        {
            throw new InvalidDataException(
                $"Package '{manifest.Id}' requires engine version '{effectiveMinimum}' or newer, but the running engine is '{EngineCompatibility.CurrentVersionText}'.");
        }

        return effectiveMinimum;
    }

    private void LoadPackage(string manifestPath, PackageSource source, PackageManifest manifest)
    {
        if (m_LoadedPackages.ContainsKey(manifest.Id)) return;

        string engineMinimumVersion = manifest.Engine?.MinVersion ?? manifest.EngineVersion ?? string.Empty;

        KernelLog.Info($"[PackageSubsystem] Loading Package: {manifest.Name} ({manifest.Id})");

        int packageOrder = m_LoadOrder.Count;
        PackageLoadContext? loadContext = null;
        IPackageEntry? entry = null;
        bool entryLoadCompleted = false;
        var nativeRuntimes = new List<LoadedNativeRuntime>();
        using var serviceRegistrationScope = EngineKernel.Instance.Services is ServiceRegistry registry
            ? registry.BeginPackageRegistration(manifest.Id)
            : null;
        using var subsystemRegistrationScope = EngineKernel.Instance.BeginPackageSubsystemRegistration(
            manifest.Id,
            packageOrder);

        try
        {
            string rootPath = Path.GetFullPath(Path.GetDirectoryName(manifestPath)!);
            Assembly? assembly = null;
            object? entryInstance = null;
            string entryAssembly = manifest.Entry?.Assembly ?? string.Empty;
            string entryClass = manifest.Entry?.Class ?? string.Empty;

            if (string.Equals(entryAssembly, "ArisenKernel.dll", StringComparison.OrdinalIgnoreCase))
            {
                assembly = typeof(PackageSubsystem).Assembly;
                KernelLog.Info($"[PackageSubsystem] Package '{manifest.Id}' entry assembly '{entryAssembly}' uses kernel assembly context.");
            }
            else if (!string.IsNullOrEmpty(entryAssembly))
            {
                string assemblyPath = ResolveEntryAssemblyPath(rootPath, entryAssembly);
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    throw new FileNotFoundException($"Entry assembly '{entryAssembly}' was not found for package '{manifest.Id}'.");
                }

                var loadPolicy = GetAssemblyLoadPolicy(assemblyPath);
                if (loadPolicy == PackageAssemblyLoadPolicy.DefaultContext)
                {
                    assembly = Assembly.LoadFrom(assemblyPath);
                    KernelLog.Info($"[PackageSubsystem] Package '{manifest.Id}' entry assembly '{entryAssembly}' loaded in default context.");
                }
                else
                {
                    loadContext = new PackageLoadContext(assemblyPath);
                    assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
                    KernelLog.Info($"[PackageSubsystem] Package '{manifest.Id}' entry assembly '{entryAssembly}' loaded in isolated collectible context.");
                }
            }

            if (assembly != null && !string.IsNullOrEmpty(entryClass))
            {
                var type = assembly.GetType(entryClass, throwOnError: false);
                if (type == null)
                {
                    throw new TypeLoadException($"Entry class '{entryClass}' was not found in assembly '{entryAssembly}' for package '{manifest.Id}'.");
                }

                entryInstance = Activator.CreateInstance(type);
                if (entryInstance is not IPackageEntry packageEntry)
                {
                    throw new InvalidOperationException($"Entry class '{entryClass}' for package '{manifest.Id}' must implement {nameof(IPackageEntry)}.");
                }

                entry = packageEntry;
                entry.OnLoad(EngineKernel.Instance.Services);
                entryLoadCompleted = true;
            }

            var packageInfo = new ArisenPackageInfo
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                Type = manifest.Type,
                RootPath = rootPath,
                Source = source,
                EngineVersion = engineMinimumVersion,
                Dependencies = manifest.Dependencies ?? new(),
                ProvidedServices = EnumerateServiceContracts(manifest.Services?.Provides).ToList(),
                RequiredServices = EnumerateServiceContracts(manifest.Services?.Requires, includeDeferredOrOptional: false).ToList(),
                Assembly = assembly,
                EntryInstance = entryInstance
            };

            nativeRuntimes = LoadNativeRuntimes(manifest);
            RegisterPackageSubsystems(manifest, assembly, packageOrder);
            ValidateProvidedServices(manifest);

            if (nativeRuntimes.Count > 0)
            {
                m_LoadedNativeRuntimes[manifest.Id] = nativeRuntimes;
            }

            if (loadContext != null)
            {
                m_LoadContexts[manifest.Id] = loadContext;
            }

            m_LoadedPackages[manifest.Id] = packageInfo;
            m_LoadOrder.Add(manifest.Id);
        }
        catch (Exception e)
        {
            string entryClass = manifest.Entry?.Class ?? "<none>";
            KernelLog.Error($"[PackageSubsystem] Error loading package {manifest.Id} entry {entryClass}: {e.Message}");
            m_LoadedPackages.Remove(manifest.Id);
            m_LoadedNativeRuntimes.Remove(manifest.Id);
            m_LoadContexts.Remove(manifest.Id);
            m_LoadOrder.Remove(manifest.Id);
            var rollbackErrors = new List<Exception>();
            if (entryLoadCompleted && entry != null)
            {
                TryUnloadEntry(manifest.Id, entry, rollbackErrors);
            }

            ShutdownNativeRuntimes(manifest.Id, nativeRuntimes, rollbackErrors);
            UnregisterPackageServicesAndSubsystems(manifest.Id, rollbackErrors);
            if (loadContext != null)
            {
                TryUnloadContext(manifest.Id, loadContext, rollbackErrors);
            }

            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, e);
                throw new AggregateException(
                    $"Package '{manifest.Id}' load failed and rollback reported additional errors.",
                    rollbackErrors);
            }

            throw;
        }
    }

    private List<LoadedNativeRuntime> LoadNativeRuntimes(PackageManifest manifest)
    {
        var loadedRuntimes = new List<LoadedNativeRuntime>();
        if (manifest.NativeRuntimes == null || manifest.NativeRuntimes.Count == 0) return loadedRuntimes;

        IntPtr provisionalHandle = IntPtr.Zero;
        NativeRuntimeBlock? provisionalRuntime = null;
        bool provisionalInitCompleted = false;
        try
        {
            foreach (var runtime in EnumerateNativeRuntimes(manifest))
            {
                if (string.IsNullOrWhiteSpace(runtime.InitExport) && string.IsNullOrWhiteSpace(runtime.ShutdownExport))
                {
                    continue;
                }

                provisionalRuntime = runtime;
                provisionalHandle = m_NativeRuntimeApi.Load(manifest.Id, runtime.Path);
                KernelLog.Info(
                    $"[PackageSubsystem] Loaded native runtime '{Path.GetFileName(runtime.Path)}' for package '{manifest.Id}'.");

                if (!string.IsNullOrWhiteSpace(runtime.InitExport))
                {
                    InvokeNativeLifecycleExport(
                        manifest.Id,
                        provisionalHandle,
                        runtime.Path,
                        runtime.InitExport,
                        "init");
                }

                provisionalInitCompleted = true;
                loadedRuntimes.Add(new LoadedNativeRuntime(
                    runtime.Path,
                    provisionalHandle,
                    runtime.ShutdownExport));
                provisionalHandle = IntPtr.Zero;
                provisionalRuntime = null;
                provisionalInitCompleted = false;
            }
        }
        catch (Exception loadError)
        {
            var rollbackErrors = new List<Exception>();
            if (provisionalHandle != IntPtr.Zero)
            {
                if (provisionalInitCompleted &&
                    provisionalRuntime != null &&
                    !string.IsNullOrWhiteSpace(provisionalRuntime.ShutdownExport))
                {
                    try
                    {
                        InvokeNativeLifecycleExport(
                            manifest.Id,
                            provisionalHandle,
                            provisionalRuntime.Path,
                            provisionalRuntime.ShutdownExport,
                            "shutdown");
                    }
                    catch (Exception shutdownError)
                    {
                        rollbackErrors.Add(shutdownError);
                    }
                }

                TryFreeNativeRuntime(manifest.Id, provisionalRuntime?.Path ?? "<unknown>", provisionalHandle, rollbackErrors);
            }

            ShutdownNativeRuntimes(manifest.Id, loadedRuntimes, rollbackErrors);
            if (rollbackErrors.Count > 0)
            {
                rollbackErrors.Insert(0, loadError);
                throw new AggregateException(
                    $"Package '{manifest.Id}' native runtime load failed and rollback reported additional errors.",
                    rollbackErrors);
            }

            throw;
        }

        return loadedRuntimes;
    }

    private void InvokeNativeLifecycleExport(
        string packageId,
        IntPtr libraryHandle,
        string libraryPath,
        string exportName,
        string phase)
    {
        int result = m_NativeRuntimeApi.InvokeLifecycle(
            packageId,
            libraryHandle,
            libraryPath,
            exportName,
            phase);
        if (result != 0)
        {
            throw new InvalidOperationException($"Package '{packageId}' native runtime '{libraryPath}' {phase} export '{exportName}' returned error code {result}.");
        }

        KernelLog.Info($"[PackageSubsystem] Native {phase} hook '{exportName}' succeeded for package '{packageId}'.");
    }

    private static IEnumerable<NativeRuntimeBlock> EnumerateNativeRuntimes(PackageManifest manifest)
    {
        foreach (var ridEntry in manifest.NativeRuntimes ?? new Dictionary<string, List<JsonElement>>())
        {
            if (!string.Equals(ridEntry.Key, DefaultRuntimeIdentifier, StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var element in ridEntry.Value)
            {
                if (TryReadNativeRuntime(element, out var runtime))
                {
                    yield return runtime;
                }
            }
        }
    }

    private static bool TryReadNativeRuntime(JsonElement element, out NativeRuntimeBlock runtime)
    {
        runtime = new NativeRuntimeBlock();

        if (element.ValueKind == JsonValueKind.String)
        {
            runtime.Path = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(runtime.Path);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        runtime.Path = ReadStringProperty(element, "path") ?? ReadStringProperty(element, "name") ?? string.Empty;
        runtime.InitExport = ReadStringProperty(element, "initExport");
        runtime.ShutdownExport = ReadStringProperty(element, "shutdownExport");
        return !string.IsNullOrWhiteSpace(runtime.Path);
    }

    private static string? ReadStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static PackageAssemblyLoadPolicy GetAssemblyLoadPolicy(string assemblyPath)
    {
        string fullAssemblyPath = Path.GetFullPath(assemblyPath);
        string baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        return fullAssemblyPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase)
            ? PackageAssemblyLoadPolicy.DefaultContext
            : PackageAssemblyLoadPolicy.IsolatedCollectibleContext;
    }

    private static string ResolveEntryAssemblyPath(string rootPath, string entryAssembly)
    {
        string binPath = Path.Combine(AppContext.BaseDirectory, entryAssembly);
        if (File.Exists(binPath)) return binPath;

        string rootAssemblyPath = Path.Combine(rootPath, entryAssembly);
        if (File.Exists(rootAssemblyPath)) return rootAssemblyPath;

        string managedAssemblyPath = Path.Combine(rootPath, "Managed", entryAssembly);
        if (File.Exists(managedAssemblyPath)) return managedAssemblyPath;

        return string.Empty;
    }

            private void RegisterPackageSubsystems(PackageManifest manifest, Assembly? assembly, int packageOrder)
    {
        if (manifest.Subsystems == null || manifest.Subsystems.Count == 0) return;

        if (assembly == null)
        {
            throw new InvalidOperationException($"Package '{manifest.Id}' declares subsystems but has no managed assembly to scan.");
        }

        foreach (var subsystemMetadata in manifest.Subsystems)
        {
            if (string.IsNullOrWhiteSpace(subsystemMetadata.Class))
            {
                throw new InvalidOperationException($"Package '{manifest.Id}' declares a subsystem with an empty class name.");
            }

            var subsystemType = assembly.GetType(subsystemMetadata.Class, throwOnError: false);
            if (subsystemType == null)
            {
                throw new TypeLoadException($"Subsystem class '{subsystemMetadata.Class}' was not found for package '{manifest.Id}'.");
            }

            object? instance = Activator.CreateInstance(subsystemType);
            if (instance is not IEngineSubsystem subsystem)
            {
                throw new InvalidOperationException($"Subsystem class '{subsystemMetadata.Class}' for package '{manifest.Id}' must implement {nameof(IEngineSubsystem)}.");
            }

                        if (!Enum.TryParse<EnginePhase>(subsystemMetadata.Phase, ignoreCase: true, out var initPhase))
            {
                throw new InvalidOperationException($"Subsystem '{subsystemMetadata.Class}' for package '{manifest.Id}' declares invalid phase '{subsystemMetadata.Phase}'.");
            }

                        EngineKernel.Instance.RegisterSubsystem(
                subsystem,
                manifest.Id,
                packageOrder,
                subsystemMetadata.Class,
                initPhase,
                subsystemMetadata.Priority);

            using var registrationScope = EngineKernel.Instance.Services is ServiceRegistry registry
                ? registry.BeginPackageRegistration(manifest.Id)
                : null;
            EngineKernel.Instance.Services.RegisterService(subsystemType, subsystem);

            KernelLog.Info($"[PackageSubsystem] Registered subsystem {subsystemMetadata.Class} from package {manifest.Id}.");
        }
    }

    private void ValidateProvidedServices(PackageManifest manifest)
    {
        foreach (var contractName in EnumerateServiceContracts(manifest.Services?.Provides, includeDeferredOrOptional: false))
        {
            bool wasRegisteredByPackage = EngineKernel.Instance.Services.GetRegisteredServices().Any(service =>
                ServiceContractMatches(service.ContractName, contractName)
                && string.Equals(service.ProviderPackageId, manifest.Id, StringComparison.OrdinalIgnoreCase));

            if (!wasRegisteredByPackage)
            {
                throw new InvalidOperationException($"Package '{manifest.Id}' declares provided service '{contractName}', but it was not registered during OnLoad().");
            }
        }
    }

    private void ValidateRequiredServices()
    {
        foreach (var package in m_LoadedPackages.Values)
        {
            foreach (var contractName in package.RequiredServices)
            {
                if (!EngineKernel.Instance.Services.IsServiceRegistered(contractName))
                {
                    throw new InvalidOperationException($"Package '{package.Id}' requires service '{contractName}', but it is not registered after package mounting.");
                }
            }
        }
    }

        private static IEnumerable<string> EnumerateServiceContracts(List<JsonElement>? serviceElements, bool includeDeferredOrOptional = true)
    {
        if (serviceElements == null) yield break;

        foreach (var element in serviceElements)
        {
            string? contractName = null;
            bool isDeferredOrOptional = false;
            if (element.ValueKind == JsonValueKind.String)
            {
                contractName = element.GetString();
            }
            else if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("interface", out var interfaceElement)
                && interfaceElement.ValueKind == JsonValueKind.String)
            {
                contractName = interfaceElement.GetString();
                isDeferredOrOptional = IsTrue(element, "deferred") || IsTrue(element, "optional");
            }

            if (!includeDeferredOrOptional && isDeferredOrOptional) continue;

            if (!string.IsNullOrWhiteSpace(contractName))
            {
                yield return contractName;
            }
        }
    }

    private static bool IsTrue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static bool ServiceContractMatches(string registeredContractName, string declaredContractName)
    {
                if (string.Equals(registeredContractName, declaredContractName, StringComparison.Ordinal)) return true;
        if (declaredContractName.StartsWith(registeredContractName + ",", StringComparison.Ordinal)) return true;

        int lastDot = registeredContractName.LastIndexOf('.');
        string shortName = lastDot >= 0 ? registeredContractName[(lastDot + 1)..] : registeredContractName;
        return string.Equals(shortName, declaredContractName, StringComparison.Ordinal);
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

    public IEnumerable<ArisenPackageInfo> GetLoadedPackagesInOrder()
    {
        foreach (var packageId in m_LoadOrder)
        {
            if (m_LoadedPackages.TryGetValue(packageId, out var package))
            {
                yield return package;
            }
        }
    }

    private void RollbackLoadedPackagesTo(int initialPackageCount, List<Exception> errors)
    {
        for (int i = m_LoadOrder.Count - 1; i >= initialPackageCount; i--)
        {
            string packageId = m_LoadOrder[i];
            if (m_LoadedPackages.TryGetValue(packageId, out ArisenPackageInfo? package))
            {
                UnloadPackage(packageId, package, errors);
            }
            else
            {
                m_LoadOrder.RemoveAt(i);
            }
        }
    }

    private void UnloadPackage(string packageId, ArisenPackageInfo package, List<Exception> errors)
    {
        if (package.EntryInstance is IPackageEntry entry)
        {
            TryUnloadEntry(packageId, entry, errors);
        }

        if (m_LoadedNativeRuntimes.TryGetValue(packageId, out List<LoadedNativeRuntime>? nativeRuntimes))
        {
            ShutdownNativeRuntimes(packageId, nativeRuntimes, errors);
        }

        UnregisterPackageServicesAndSubsystems(packageId, errors);

        m_LoadedPackages.Remove(packageId);
        m_LoadedNativeRuntimes.Remove(packageId);
        m_LoadOrder.Remove(packageId);

        if (m_LoadContexts.Remove(packageId, out PackageLoadContext? loadContext))
        {
            TryUnloadContext(packageId, loadContext, errors);
        }
    }

    private void TryUnloadEntry(
        string packageId,
        IPackageEntry entry,
        List<Exception> errors)
    {
        int packageOrder = m_LoadOrder.IndexOf(packageId);
        if (packageOrder < 0) packageOrder = int.MaxValue;
        using var serviceRegistrationScope = EngineKernel.Instance.Services is ServiceRegistry registry
            ? registry.BeginPackageRegistration(packageId)
            : null;
        using var subsystemRegistrationScope = EngineKernel.Instance.BeginPackageSubsystemRegistration(
            packageId,
            packageOrder);

        try
        {
            entry.OnUnload(EngineKernel.Instance.Services);
        }
        catch (Exception error)
        {
            KernelLog.Error($"[PackageSubsystem] Error unloading package {packageId}: {error.Message}");
            AddFailure(errors, error);
        }
    }

    private void ShutdownNativeRuntimes(
        string packageId,
        List<LoadedNativeRuntime> nativeRuntimes,
        List<Exception> errors)
    {
        for (int nativeIndex = nativeRuntimes.Count - 1; nativeIndex >= 0; nativeIndex--)
        {
            LoadedNativeRuntime nativeRuntime = nativeRuntimes[nativeIndex];
            try
            {
                if (!string.IsNullOrWhiteSpace(nativeRuntime.ShutdownExport))
                {
                    InvokeNativeLifecycleExport(
                        packageId,
                        nativeRuntime.LibraryHandle,
                        nativeRuntime.Path,
                        nativeRuntime.ShutdownExport,
                        "shutdown");
                }
            }
            catch (Exception error)
            {
                KernelLog.Error(
                    $"[PackageSubsystem] Error running native shutdown for package {packageId}: {error.Message}");
                AddFailure(errors, error);
            }
            finally
            {
                TryFreeNativeRuntime(
                    packageId,
                    nativeRuntime.Path,
                    nativeRuntime.LibraryHandle,
                    errors);
            }
        }

        nativeRuntimes.Clear();
    }

    private void TryFreeNativeRuntime(
        string packageId,
        string runtimePath,
        IntPtr libraryHandle,
        List<Exception> errors)
    {
        try
        {
            m_NativeRuntimeApi.Free(libraryHandle);
        }
        catch (Exception error)
        {
            KernelLog.Error(
                $"[PackageSubsystem] Error freeing native runtime '{runtimePath}' for package {packageId}: {error.Message}");
            AddFailure(errors, error);
        }
    }

    private static void UnregisterPackageServicesAndSubsystems(
        string packageId,
        List<Exception> errors)
    {
        if (EngineKernel.Instance.Services is ServiceRegistry registry)
        {
            try
            {
                int removedServiceCount = registry.UnregisterServicesProvidedByPackage(packageId);
                if (removedServiceCount > 0)
                {
                    KernelLog.Info(
                        $"[PackageSubsystem] Unregistered {removedServiceCount} service(s) provided by package {packageId}.");
                }
            }
            catch (Exception error)
            {
                KernelLog.Error(
                    $"[PackageSubsystem] Error unregistering services for package {packageId}: {error.Message}");
                AddFailure(errors, error);
            }
        }

        try
        {
            int removedSubsystemCount = EngineKernel.Instance.UnregisterSubsystemsProvidedByPackage(packageId);
            if (removedSubsystemCount > 0)
            {
                KernelLog.Info(
                    $"[PackageSubsystem] Unregistered {removedSubsystemCount} subsystem(s) provided by package {packageId}.");
            }
        }
        catch (Exception error)
        {
            KernelLog.Error(
                $"[PackageSubsystem] Error unregistering subsystems for package {packageId}: {error.Message}");
            AddFailure(errors, error);
        }
    }

    private static void TryUnloadContext(
        string packageId,
        PackageLoadContext loadContext,
        List<Exception> errors)
    {
        try
        {
            loadContext.Unload();
        }
        catch (Exception error)
        {
            KernelLog.Error(
                $"[PackageSubsystem] Error unloading managed context for package {packageId}: {error.Message}");
            AddFailure(errors, error);
        }
    }

    private static void AddFailure(List<Exception> errors, Exception error)
    {
        if (error is AggregateException aggregate)
        {
            errors.AddRange(aggregate.Flatten().InnerExceptions);
            return;
        }

        errors.Add(error);
    }

    public void Shutdown()
    {
        if (m_LoadOrder.Count == 0)
        {
            if (m_ShutdownFailure != null) throw m_ShutdownFailure;
            return;
        }

        var errors = new List<Exception>();
        RollbackLoadedPackagesTo(0, errors);

        if (errors.Count > 0)
        {
            m_ShutdownFailure = new AggregateException(
                "Package shutdown completed with one or more cleanup errors.",
                errors);
            throw m_ShutdownFailure;
        }

        m_ShutdownFailure = null;
    }

    public void RegisterLoadedPackage(ArisenPackageInfo info)
    {
        if (info == null || string.IsNullOrEmpty(info.Id)) return;
        m_LoadedPackages[info.Id] = info;
        if (!m_LoadOrder.Contains(info.Id)) m_LoadOrder.Add(info.Id);
    }

    public void Dispose() => Shutdown();

    private enum PackageAssemblyLoadPolicy
    {
        DefaultContext,
        IsolatedCollectibleContext
    }

    private const string DefaultRuntimeIdentifier = "win-x64";

    private sealed record LoadedNativeRuntime(string Path, IntPtr LibraryHandle, string? ShutdownExport);

    private sealed class NativeRuntimeBlock
    {
        public string Path { get; set; } = string.Empty;
        public string? InitExport { get; set; }
        public string? ShutdownExport { get; set; }
    }

    private class PackageManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Type { get; set; } = "managed";
        public PackageEntryBlock? Entry { get; set; }
        public PackageEngineCompatibilityBlock? Engine { get; set; }
        public string? EngineVersion { get; set; }
        public Dictionary<string, string>? Dependencies { get; set; }
        public PackageServicesBlock? Services { get; set; }
        public List<PackageSubsystemBlock>? Subsystems { get; set; }
        public Dictionary<string, List<JsonElement>>? NativeRuntimes { get; set; }
    }

    private class PackageEngineCompatibilityBlock
    {
        public string? MinVersion { get; set; }
    }

    private class PackageSubsystemBlock
    {
        public string Class { get; set; } = string.Empty;
        public string Phase { get; set; } = "Init";
        public int Priority { get; set; } = 100;
        public List<string>? EnabledProfiles { get; set; }
        public List<JsonElement>? RequiresServices { get; set; }
    }

    private class PackageServicesBlock
    {
        public List<JsonElement>? Provides { get; set; }
        public List<JsonElement>? Requires { get; set; }
    }

    private class PackageEntryBlock
    {
        public string Assembly { get; set; } = string.Empty;
        public string Class { get; set; } = string.Empty;
    }
}

internal sealed class NativePackageRuntimeApi : INativePackageRuntimeApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NativeLifecycleCallback();

    public IntPtr Load(string packageId, string runtimePath)
    {
        string libraryPath = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(runtimePath));
        if (!File.Exists(libraryPath))
        {
            throw new FileNotFoundException(
                $"Package '{packageId}' declares native lifecycle hooks for '{runtimePath}', but deployed library '{libraryPath}' was not found.",
                libraryPath);
        }

        return NativeLibrary.Load(libraryPath);
    }

    public int InvokeLifecycle(
        string packageId,
        IntPtr libraryHandle,
        string runtimePath,
        string exportName,
        string phase)
    {
        if (!NativeLibrary.TryGetExport(libraryHandle, exportName, out IntPtr exportPtr))
        {
            throw new EntryPointNotFoundException(
                $"Package '{packageId}' native runtime '{runtimePath}' declares {phase} export '{exportName}', but the export was not found.");
        }

        var callback = Marshal.GetDelegateForFunctionPointer<NativeLifecycleCallback>(exportPtr);
        return callback();
    }

    public void Free(IntPtr libraryHandle)
    {
        NativeLibrary.Free(libraryHandle);
    }
}

