using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArisenKernel.Contracts;
using ArisenKernel.Diagnostics;
using ArisenKernel.Lifecycle;
using ArisenKernel.Services;
using ArisenKernel.Packages;

namespace ArisenKernel.Lifecycle;

public static class EngineBootstrapper
{
    public static void Run(string[] args)
    {
        KernelLog.Info("=== Arisen Engine Bootstrapper ===");
        
        string workspacePath = "";
        string entryPackage = "";
        string profile = "Development";
        bool profileSpecified = false;
        bool workspaceSpecified = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--workspace" && i + 1 < args.Length) { workspacePath = args[i + 1]; workspaceSpecified = true; }
            if (args[i] == "--entry" && i + 1 < args.Length) entryPackage = args[i + 1];
            if (args[i] == "--profile" && i + 1 < args.Length) { profile = args[i + 1]; profileSpecified = true; }
        }

        // B18: Try to load from launch.config.json if located in the binary folder (Explicit configuration wins over deduction)
        string configPath = Path.Combine(AppContext.BaseDirectory, "launch.config.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var configDoc = JsonDocument.Parse(File.ReadAllText(configPath));
                var root = configDoc.RootElement;
                if (!profileSpecified && root.TryGetProperty("Profile", out var pProp)) profile = pProp.GetString() ?? profile;
                if (!workspaceSpecified && root.TryGetProperty("Workspace", out var wProp)) workspacePath = wProp.GetString() ?? workspacePath;
            }
            catch { /* Skip and fall back to deduction */ }
        }

        if (string.IsNullOrEmpty(workspacePath))
        {
            // NEW: In generated projects, we are in .arisen/bin/{profile}/{config}/
            // .. (config) -> .. (profile) -> .. (bin) -> .. (.arisen) -> Workspace Root
            workspacePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            KernelLog.InfoFormat("[Host] No --workspace provided. Deducing from location: {0}", workspacePath);
        }

        // 1. Initialize Kernel and Core Project Subsystem
        var kernel = EngineKernel.Instance;
        var registry = kernel.Services;
        
        var projectSubsystem = new ProjectSubsystem();
        registry.RegisterService<ProjectSubsystem>(projectSubsystem);
        projectSubsystem.LoadFromWorkspace(workspacePath);

        // B15: Initialize PackageSubsystem to track all loaded packages for other systems (like the Editor)
        var packageSubsystem = new PackageSubsystem();
        kernel.RegisterSubsystem(packageSubsystem);

        string manifestPath = Path.Combine(workspacePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            KernelLog.FatalFormat("[Host] FATAL ERROR: Cannot find manifest.json at {0}", manifestPath);
            Environment.Exit(1);
        }

        KernelLog.InfoFormat("[Host] Reading Workspace Manifest: {0}", manifestPath);
        var manifestJson = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var packagesElement = manifestJson.RootElement.GetProperty("Packages");
        
        List<string> packageUrls = new();
        void AddPackages(JsonElement element)
        {
            foreach (var pkg in element.EnumerateArray())
            {
                var url = pkg.GetProperty("Url").GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    if (url.StartsWith("file://"))
                    {
                        string localPath = url.Substring(7);
                        if (Path.IsPathRooted(localPath)) packageUrls.Add(localPath);
                        else packageUrls.Add(Path.Combine(workspacePath, localPath));
                    }
                    else
                    {
                        // TODO: Handle cache/URL packages
                        packageUrls.Add(url);
                    }
                }
            }
        }

        AddPackages(packagesElement);

        // Load Profile Packages
        if (manifestJson.RootElement.TryGetPropertyIC("Profiles", out var profilesElement))
        {
            if (profilesElement.TryGetPropertyIC(profile, out var profileDefinition))
            {
                KernelLog.InfoFormat("[Host] Loading Profile: {0}", profile);
                
                // NEW: Handle ProfileDefinition object (IsEditor, Packages, etc)
                if (profileDefinition.ValueKind == JsonValueKind.Object)
                {
                    if (profileDefinition.TryGetPropertyIC("Packages", out var profilePackages))
                    {
                        AddPackages(profilePackages);
                    }
                }
                else if (profileDefinition.ValueKind == JsonValueKind.Array)
                {
                    // Legacy support for raw package arrays in profiles
                    AddPackages(profileDefinition);
                }
            }
            else if (profile != "Development" && profile != "Production")
            {
                KernelLog.WarningFormat("[Host] WARNING: Profile '{0}' not found in manifest.json.", profile);
            }
        }

        // B11: Check for resolved manifest to skip runtime resolution and use topological order
        // PRIORITY 1: Local manifest.resolved.json co-located with binary (Modernized approach)
        string resolvedManifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.resolved.json");
        
        // PRIORITY 2: Fallback to root naming convention for legacy/debug support
        if (!File.Exists(resolvedManifestPath))
        {
            resolvedManifestPath = Path.Combine(workspacePath, $"manifest.resolved.{profile}.json");
        }

        if (File.Exists(resolvedManifestPath))
        {
            try
            {
                KernelLog.InfoFormat("[Host] Found Resolved Manifest: {0}. Using build-time topological sort.", resolvedManifestPath);
                var resolvedJson = JsonDocument.Parse(File.ReadAllText(resolvedManifestPath));
                if (resolvedJson.RootElement.TryGetProperty("ResolvedPackages", out var resolvedPkgs))
                {
                    packageUrls.Clear(); // Switch to the sorted list
                    foreach (var pkg in resolvedPkgs.EnumerateArray())
                    {
                        var url = pkg.GetProperty("Url").GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            if (url.StartsWith("file://"))
                            {
                                string localPath = url.Substring(7);
                                // For local paths in a resolved manifest, they are relative to the manifest file itself
                                string manifestDir = Path.GetDirectoryName(resolvedManifestPath)!;
                                if (Path.IsPathRooted(localPath)) packageUrls.Add(localPath);
                                else packageUrls.Add(Path.GetFullPath(Path.Combine(manifestDir, localPath)));
                            }
                            else packageUrls.Add(url);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KernelLog.WarningFormat("[Host] Failed to parse resolved manifest '{0}': {1}. Falling back to manifest.json order.", resolvedManifestPath, ex.Message);
            }
        }

        // 2. Load Topologically (Preferring build-time resolved manifest)
        KernelLog.Info("[Host] Mounting Packages...");
        
        foreach (var pUrl in packageUrls)
        {
            string pJsonPath = Path.Combine(pUrl, "package.json");
            if (!File.Exists(pJsonPath)) continue;

            var pDoc = JsonDocument.Parse(File.ReadAllText(pJsonPath));
            var root = pDoc.RootElement;
            string id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : root.GetProperty("name").GetString();
            
            // Native Runtimes
            if (root.TryGetProperty("nativeRuntimes", out var nativeBlock) && nativeBlock.TryGetProperty("win-x64", out var win64Block))
            {
                foreach (var dll in win64Block.EnumerateArray())
                {
                    string nativeName = dll.GetString();
                    KernelLog.InfoFormat("[Host] Mapping Native C++ Payload: {0} for {1}", nativeName, id);
                    // NativeLibrary.Load(nativeName); // Actually deferred to execution or .arisen/bin/ output directory loader
                }
            }
            
        // B5+A5: Read nested entry: { assembly, class } schema (standardized format)
        if (root.TryGetPropertyIC("entry", out var entryObj) && entryObj.ValueKind == JsonValueKind.Object)
        {
            string asmName = entryObj.TryGetPropertyIC("assembly", out var asmProp) ? asmProp.GetString() ?? "" : "";
            string entryClassName = entryObj.TryGetPropertyIC("class", out var clsProp) ? clsProp.GetString() ?? "" : "";
            
            if (!string.IsNullOrEmpty(asmName))
            {
                KernelLog.InfoFormat("[Host] Booting Managed Entry: {0} for {1}", asmName, id);
                try
                {
                    // Prefer central bin folder (where Host is) for generated projects
                    string binDir = AppContext.BaseDirectory;
                    string fullPath = Path.Combine(binDir, asmName);
                    
                    // Fallback to source Managed folder if not found in bin
                    if (!File.Exists(fullPath))
                    {
                        fullPath = Path.Combine(pUrl, "Managed", asmName);
                    }

                    if (File.Exists(fullPath))
                    {
                        Assembly asm = Assembly.LoadFrom(fullPath);
                        object? entryInstance = null;
                        if (!string.IsNullOrEmpty(entryClassName))
                        {
                            Type t = asm.GetType(entryClassName);
                            if (t != null)
                            {
                                entryInstance = Activator.CreateInstance(t);
                                var onLoadMethod = t.GetMethod("OnLoad");
                                if (onLoadMethod != null) onLoadMethod.Invoke(entryInstance, new object[] { registry });
                            }
                        }

                        // B15: Register this successfully loaded package in the PackageSubsystem
                        var pkgInfo = new ArisenPackageInfo
                        {
                            Id = id,
                            Name = root.TryGetProperty("name", out var n) ? n.GetString() ?? id : id,
                            Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "1.0.0" : "1.0.0",
                            Type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "managed" : "managed",
                            RootPath = pUrl,
                            Assembly = asm,
                            EntryInstance = entryInstance,
                            Source = pUrl.Contains("Local") ? PackageSource.External : PackageSource.Official
                        };
                        packageSubsystem.RegisterLoadedPackage(pkgInfo);
                    }
                    else
                    {
                         KernelLog.WarningFormat("[Host] WARNING: Could not find assembly {0} in bin/ or Managed/", asmName);
                    }
                }
                catch (Exception e)
                {
                    KernelLog.ErrorFormat("[Host] Managed boot neglected/failed for {0}: {1}", id, e.Message);
                }
            }
        }
    }

    KernelLog.Info("[Host] Topological Mount Complete.");

    // 3. Fallback to registry checks for boot takeover
    if (registry.TryGetService<IApplicationHost>(out var appHost))
    {
        KernelLog.Info("[Host] Yielding main thread to IApplicationHost (Editor/UI).");
        appHost.Run(args);
    }
    else
    {
        KernelLog.Info("[Host] No IApplicationHost detected. Engaging default bare-metal Engine tick.");
        kernel.Run();
        }
    }
}

public static class JsonExtensions
{
    public static bool TryGetPropertyIC(this JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static JsonElement GetPropertyIC(this JsonElement element, string propertyName)
    {
        if (TryGetPropertyIC(element, propertyName, out var value)) return value;
        throw new KeyNotFoundException($"Property '{propertyName}' not found (case-insensitive)");
    }
}
