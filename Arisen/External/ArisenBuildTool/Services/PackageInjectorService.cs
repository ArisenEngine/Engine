using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArisenBuildTool.Models;

namespace ArisenBuildTool.Services;

public static class PackageInjectorService
{
    public static void Inject(string packageDir, string assemblyPath)
    {
        string packageJsonPath = Path.Combine(packageDir, "package.json");
        string generatedJsonPath = Path.Combine(packageDir, "package.generated.json");

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

        var generatedPackage = new GeneratedPackageMetadata
        {
            Entry = new PackageEntry
            {
                Assembly = Path.GetFileName(assemblyPath)
            }
        };

        // Hook assembly resolution to find dependencies in the same folder as the target assembly.
        ResolveEventHandler resolveHandler = (sender, args) =>
        {
            string folder = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
            string name = new AssemblyName(args.Name).Name + ".dll";
            string candidate = Path.Combine(folder, name);
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        };
        AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;

        // Scan Assembly via byte loading to avoid file locks.
        try
        {
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            Assembly assembly = Assembly.Load(assemblyBytes);

            var subsystems = new List<PackageSubsystem>();
            var provides = new List<PackageServiceProvider>();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
            {
                // Discovery Phase 1: Identify Package Entry Class.
                bool implementsEntry = type.GetInterfaces().Any(i => i.Name == "IPackageEntry");
                if (implementsEntry)
                {
                    generatedPackage.Entry!.Class = type.FullName;
                    Console.WriteLine($"[ArisenBuildTool] Discovered Entry Class: {type.FullName}");

                    // If it also implements IApplicationHost, automatically register it as a service.
                                        if (type.GetInterfaces().FirstOrDefault(i => i.Name == "IApplicationHost") is { } applicationHostInterface)
                    {
                        provides.Add(new PackageServiceProvider { Interface = applicationHostInterface.FullName ?? applicationHostInterface.Name, Priority = 100 });
                        Console.WriteLine($"[ArisenBuildTool] Automated Service Discovery: {applicationHostInterface.FullName ?? applicationHostInterface.Name}");
                    }
                }

                // Discovery Phase 2: Handle Subsystems via Attributes.
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

                // Discovery Phase 3: Handle Service Providers via Attributes.
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

            if (subsystems.Count > 0) generatedPackage.Subsystems = subsystems;

            if (provides.Count > 0)
            {
                generatedPackage.Services ??= new PackageServices();
                generatedPackage.Services.Provides = provides.Select(p => JsonSerializer.SerializeToElement(p)).ToList();
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            string updatedJson = JsonSerializer.Serialize(generatedPackage, options);
            File.WriteAllText(generatedJsonPath, updatedJson);

            Console.WriteLine($"ArisenBuildTool Inject Success: Wrote {Path.GetFileName(packageDir)}/package.generated.json with {subsystems.Count} subsystems.");
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

    private sealed class GeneratedPackageMetadata
    {
        [JsonPropertyName("entry")]
        public PackageEntry? Entry { get; set; }

        [JsonPropertyName("services")]
        public PackageServices? Services { get; set; }

        [JsonPropertyName("subsystems")]
        public List<PackageSubsystem>? Subsystems { get; set; }
    }
}
