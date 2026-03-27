using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class PackageInjectorService
{
    public static void Inject(string packageDir, string assemblyPath)
    {
        string packageJsonPath = Path.Combine(packageDir, "package.json");
        
        if (!File.Exists(packageJsonPath))
        {
            Console.WriteLine($"ArisenBuildTool Inject Error: package.json not found at {packageJsonPath}");
            return;
        }

        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"ArisenBuildTool Inject Error: Assembly not found at {assemblyPath}");
            return;
        }

        PackageManifest pkg;
        try
        {
            string rawJson = File.ReadAllText(packageJsonPath);
            pkg = JsonSerializer.Deserialize<PackageManifest>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ArisenBuildTool Inject Error: Malformed package.json. {ex.Message}");
            return;
        }

        pkg.Entry ??= new PackageEntry();
        pkg.Entry.Assembly = Path.GetFileName(assemblyPath);

        // Hook assembly resolution to find dependencies in the same folder as the target assembly
        ResolveEventHandler resolveHandler = (sender, args) =>
        {
            string folder = Path.GetDirectoryName(assemblyPath) ?? "";
            string name = new AssemblyName(args.Name).Name + ".dll";
            string candidate = Path.Combine(folder, name);
            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            return null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;

        // Scan Assembly via byte loading to avoid absolutely any File Locks
        try
        {
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            Assembly assembly = Assembly.Load(assemblyBytes);

            var subsystems = new List<PackageSubsystem>();
            var provides = new List<PackageServiceProvider>();

            foreach (var type in assembly.GetTypes())
            {
                // Find IEngineSubsystem logic
                var subsystemAttr = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "EngineSubsystemAttribute");
                if (subsystemAttr != null)
                {
                    string phase = "Init";
                    int priority = 100;
                    
                    if (subsystemAttr.ConstructorArguments.Count > 0)
                        phase = subsystemAttr.ConstructorArguments[0].Value?.ToString() ?? "Init";
                    if (subsystemAttr.ConstructorArguments.Count > 1)
                        if (subsystemAttr.ConstructorArguments[1].Value is int prio) priority = prio;

                    subsystems.Add(new PackageSubsystem
                    {
                        Class = type.FullName ?? type.Name,
                        Phase = phase,
                        Priority = priority
                    });
                }
                
                // Find ServiceProviders logic
                var serviceAttr = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "EngineServiceAttribute");
                if (serviceAttr != null && serviceAttr.ConstructorArguments.Count > 0)
                {
                    if (serviceAttr.ConstructorArguments[0].Value is Type interfaceType)
                    {
                        provides.Add(new PackageServiceProvider
                        {
                            Interface = interfaceType.FullName ?? interfaceType.Name,
                            Priority = serviceAttr.ConstructorArguments.Count > 1 
                                && serviceAttr.ConstructorArguments[1].Value is int prio2 ? prio2 : 100
                        });
                    }
                }
            }

            if (subsystems.Count > 0) pkg.Subsystems = subsystems;
            
            if (provides.Count > 0) 
            {
                pkg.Services ??= new PackageServices();
                pkg.Services.Provides = provides.Select(p => JsonSerializer.SerializeToElement(p)).ToList();
            }

            // Save modified package.json safely without mangling existing fields we don't own
            var options = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            string updatedJson = JsonSerializer.Serialize(pkg, options);
            File.WriteAllText(packageJsonPath, updatedJson);

            Console.WriteLine($"ArisenBuildTool Inject Success: Rewrote {Path.GetFileName(packageDir)}/package.json with {subsystems.Count} subsystems.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ArisenBuildTool Inject Error during Reflection: {ex.Message}");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolveHandler;
        }
    }
}
