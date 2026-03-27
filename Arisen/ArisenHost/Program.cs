using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArisenKernel.Contracts;
using ArisenKernel.Lifecycle;
using ArisenKernel.Services;

namespace ArisenHost;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== ArisenHost Bootstrapper ===");
        
        string workspacePath = "";
        string entryPackage = "";
        string profile = "Development";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--workspace" && i + 1 < args.Length) workspacePath = args[i + 1];
            if (args[i] == "--entry" && i + 1 < args.Length) entryPackage = args[i + 1];
            if (args[i] == "--profile" && i + 1 < args.Length) profile = args[i + 1];
        }

        if (string.IsNullOrEmpty(workspacePath))
        {
            // NEW: In generated projects, we are in .arisen/bin/{profile}/{config}/
            // .. (config) -> .. (profile) -> .. (bin) -> .. (.arisen) -> Workspace Root
            workspacePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            Console.WriteLine($"[Host] No --workspace provided. Deducing from location: {workspacePath}");
        }

        string manifestPath = Path.Combine(workspacePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"[Host] FATAL ERROR: Cannot find manifest.json at {manifestPath}");
            Environment.Exit(1);
        }

        Console.WriteLine($"[Host] Reading Workspace Manifest: {manifestPath}");
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
        if (manifestJson.RootElement.TryGetProperty("Profiles", out var profilesElement))
        {
            if (profilesElement.TryGetProperty(profile, out var profilePackages))
            {
                Console.WriteLine($"[Host] Loading Profile: {profile}");
                AddPackages(profilePackages);
            }
            else if (profile != "Development" && profile != "Production")
            {
                Console.WriteLine($"[Host] WARNING: Profile '{profile}' not found in manifest.json.");
            }
        }

        // 1. Initialize Kernel
        var kernel = EngineKernel.Instance;
        var registry = kernel.Services;

        // 2. Load Topologically (Simplified parsing)
        Console.WriteLine("[Host] Mounting Packages...");
        
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
                    Console.WriteLine($"[Host] Mapping Native C++ Payload: {nativeName} for {id}");
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
                Console.WriteLine($"[Host] Booting Managed Entry: {asmName} for {id}");
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
                        if (!string.IsNullOrEmpty(entryClassName))
                        {
                            Type t = asm.GetType(entryClassName);
                            if (t != null)
                            {
                                var entryInstance = Activator.CreateInstance(t);
                                var onLoadMethod = t.GetMethod("OnLoad");
                                if (onLoadMethod != null) onLoadMethod.Invoke(entryInstance, new object[] { registry });
                            }
                        }
                    }
                    else
                    {
                         Console.WriteLine($"[Host] WARNING: Could not find assembly {asmName} in bin/ or Managed/");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Host] Managed boot neglected/failed for {id}: " + e.Message);
                }
            }
        }
    }

    Console.WriteLine("[Host] Topological Mount Complete.");

    // 3. Fallback to registry checks for boot takeover
    if (registry.TryGetService<IApplicationHost>(out var appHost))
    {
        Console.WriteLine("[Host] Yielding main thread to IApplicationHost (Editor/UI).");
        appHost.Run(args);
    }
    else
    {
        Console.WriteLine("[Host] No IApplicationHost detected. Engaging default bare-metal Engine tick.");
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
