using System.Text.Json;
using System.Reflection;
using System;
using System.Runtime.InteropServices;
using ArisenKernel.Lifecycle;
using ArisenKernel.Diagnostics;
using ArisenKernel.Services;

namespace ArisenKernel.Packages;

public class PackageSubsystem : IEngineSubsystem
{
    private readonly Dictionary<string, ArisenPackageInfo> m_LoadedPackages = new();
    private readonly List<string> m_LoadOrder = new();
    private readonly List<PackageLoadContext> m_LoadContexts = new();
    private readonly Dictionary<string, List<LoadedNativeRuntime>> m_LoadedNativeRuntimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPackageResolver m_Resolver = new DefaultPackageResolver();
    private string m_PackagesRoot = string.Empty;

    public string Name => "PackageSubsystem";
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    public void MountPackages(IEnumerable<string> packageUrls)
    {
        if (packageUrls == null) throw new ArgumentNullException(nameof(packageUrls));

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

                        LoadPackage(packageJsonPath, PackageSource.External, manifest);
        }

        ValidateRequiredServices();
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

        ValidateRequiredServices();
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
                    var loadContext = new PackageLoadContext(assemblyPath);
                    m_LoadContexts.Add(loadContext);
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
                if (entryInstance is not IPackageEntry entry)
                {
                    throw new InvalidOperationException($"Entry class '{entryClass}' for package '{manifest.Id}' must implement {nameof(IPackageEntry)}.");
                }

                using var registrationScope = EngineKernel.Instance.Services is ServiceRegistry registry
                    ? registry.BeginPackageRegistration(manifest.Id)
                    : null;

                entry.OnLoad(EngineKernel.Instance.Services);
            }

            var packageInfo = new ArisenPackageInfo
            {
                Id = manifest.Id,
                Name = manifest.Name,
                Version = manifest.Version,
                Type = manifest.Type,
                RootPath = rootPath,
                Source = source,
                EngineVersion = manifest.EngineVersion,
                Dependencies = manifest.Dependencies ?? new(),
                ProvidedServices = EnumerateServiceContracts(manifest.Services?.Provides).ToList(),
                RequiredServices = EnumerateServiceContracts(manifest.Services?.Requires, includeDeferredOrOptional: false).ToList(),
                Assembly = assembly,
                EntryInstance = entryInstance
            };

            int packageOrder = m_LoadOrder.Count;
            var nativeRuntimes = LoadNativeRuntimes(manifest);
            RegisterPackageSubsystems(manifest, assembly, packageOrder);
            ValidateProvidedServices(manifest);

            if (nativeRuntimes.Count > 0)
            {
                m_LoadedNativeRuntimes[manifest.Id] = nativeRuntimes;
            }

            m_LoadedPackages[manifest.Id] = packageInfo;
            m_LoadOrder.Add(manifest.Id);
        }
        catch (Exception e)
        {
            string entryClass = manifest.Entry?.Class ?? "<none>";
            KernelLog.Error($"[PackageSubsystem] Error loading package {manifest.Id} entry {entryClass}: {e.Message}");
            throw;
        }
    }



    private static List<LoadedNativeRuntime> LoadNativeRuntimes(PackageManifest manifest)
    {
        var loadedRuntimes = new List<LoadedNativeRuntime>();
        if (manifest.NativeRuntimes == null || manifest.NativeRuntimes.Count == 0) return loadedRuntimes;

        foreach (var runtime in EnumerateNativeRuntimes(manifest))
        {
            if (string.IsNullOrWhiteSpace(runtime.InitExport) && string.IsNullOrWhiteSpace(runtime.ShutdownExport))
            {
                continue;
            }

            string libraryPath = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(runtime.Path));
            if (!File.Exists(libraryPath))
            {
                throw new FileNotFoundException($"Package '{manifest.Id}' declares native lifecycle hooks for '{runtime.Path}', but deployed library '{libraryPath}' was not found.", libraryPath);
            }

            IntPtr libraryHandle = NativeLibrary.Load(libraryPath);
            try
            {
                KernelLog.Info($"[PackageSubsystem] Loaded native runtime '{Path.GetFileName(libraryPath)}' for package '{manifest.Id}'.");

                if (!string.IsNullOrWhiteSpace(runtime.InitExport))
                {
                    InvokeNativeLifecycleExport(manifest.Id, libraryHandle, runtime.Path, runtime.InitExport, "init");
                }

                loadedRuntimes.Add(new LoadedNativeRuntime(runtime.Path, libraryHandle, runtime.ShutdownExport));
            }
            catch
            {
                NativeLibrary.Free(libraryHandle);
                throw;
            }
        }

        return loadedRuntimes;
    }

    private static void InvokeNativeLifecycleExport(string packageId, IntPtr libraryHandle, string libraryPath, string exportName, string phase)
    {
        if (!NativeLibrary.TryGetExport(libraryHandle, exportName, out var exportPtr))
        {
            throw new EntryPointNotFoundException($"Package '{packageId}' native runtime '{libraryPath}' declares {phase} export '{exportName}', but the export was not found.");
        }

        var callback = Marshal.GetDelegateForFunctionPointer<NativeLifecycleCallback>(exportPtr);
        int result = callback();
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

    public void Shutdown()
    {
        // B19: Call OnUnload on all package entry instances in reverse mount order.
        for (int i = m_LoadOrder.Count - 1; i >= 0; i--)
        {
            string packageId = m_LoadOrder[i];
            if (!m_LoadedPackages.TryGetValue(packageId, out var pkg)) continue;

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

            if (m_LoadedNativeRuntimes.TryGetValue(packageId, out var nativeRuntimes))
            {
                for (int nativeIndex = nativeRuntimes.Count - 1; nativeIndex >= 0; nativeIndex--)
                {
                    var nativeRuntime = nativeRuntimes[nativeIndex];
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(nativeRuntime.ShutdownExport))
                        {
                            InvokeNativeLifecycleExport(pkg.Id, nativeRuntime.LibraryHandle, nativeRuntime.Path, nativeRuntime.ShutdownExport, "shutdown");
                        }
                    }
                    catch (Exception e)
                    {
                        KernelLog.Info($"[PackageSubsystem] Error running native shutdown for package {pkg.Id}: {e.Message}");
                    }
                    finally
                    {
                        NativeLibrary.Free(nativeRuntime.LibraryHandle);
                    }
                }
            }

            if (EngineKernel.Instance.Services is ServiceRegistry registry)
            {
                int removedServiceCount = registry.UnregisterServicesProvidedByPackage(pkg.Id);
                if (removedServiceCount > 0)
                {
                    KernelLog.Info($"[PackageSubsystem] Unregistered {removedServiceCount} service(s) provided by package {pkg.Id}.");
                }
            }
        }

        m_LoadOrder.Clear();
        m_LoadedPackages.Clear();
        m_LoadedNativeRuntimes.Clear();

        foreach (var context in m_LoadContexts)
        {
            context.Unload();
        }
        m_LoadContexts.Clear();
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NativeLifecycleCallback();

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
                public string EngineVersion { get; set; } = string.Empty;
        public Dictionary<string, string>? Dependencies { get; set; }
        public PackageServicesBlock? Services { get; set; }
        public List<PackageSubsystemBlock>? Subsystems { get; set; }
        public Dictionary<string, List<JsonElement>>? NativeRuntimes { get; set; }
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

