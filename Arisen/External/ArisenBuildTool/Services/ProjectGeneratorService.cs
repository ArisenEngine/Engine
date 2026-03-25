using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class ProjectGeneratorService
{
    public static void GenerateForManagedPackages(string workspaceDir, string projectsDir, string engineDir, List<PackageInfo> managedPackages, Dictionary<string, PackageInfo> packageMap, ProjectManifest manifest)
    {
        string buildExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string buildCmd = buildExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
            ? $"&quot;{buildExePath}&quot;" 
            : $"dotnet &quot;{buildExePath}&quot;";

        foreach (var package in managedPackages)
        {
            GenerateProjectFile(workspaceDir, projectsDir, engineDir, package, packageMap, buildCmd, manifest);
        }
    }

    private static void GenerateProjectFile(string workspaceDir, string projectsDir, string engineDir, PackageInfo package, Dictionary<string, PackageInfo> map, string buildCmd, ProjectManifest manifest)
    {
        string packageName = Path.GetFileName(package.DirectoryPath);
        string projectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
        string csprojDir = Path.Combine(projectsDir, projectName);
        Directory.CreateDirectory(csprojDir);
        string csprojPath = Path.Combine(csprojDir, $"{projectName}.csproj");

        using StreamWriter writer = new StreamWriter(csprojPath);
        writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        writer.WriteLine("  <PropertyGroup>");
        writer.WriteLine("    <TargetFramework>net9.0</TargetFramework>");
        writer.WriteLine("    <ImplicitUsings>enable</ImplicitUsings>");
        writer.WriteLine("    <Nullable>enable</Nullable>");
        writer.WriteLine("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
        writer.WriteLine($"    <RootNamespace>ArisenEngine.{projectName.Replace("Com.Arisen.", "").Replace("Com.User.", "")}</RootNamespace>");
        
        // Output binaries mapped uniformly into MyGame/.arisen/bin/
        writer.WriteLine("    <OutputPath>..\\..\\bin\\$(Configuration)\\</OutputPath>");
        writer.WriteLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        string[] profiles = manifest.Profiles != null && manifest.Profiles.Count > 0 
            ? manifest.Profiles.Keys.ToArray() 
            : new[] { "Development", "Production" };
            
        foreach (var profile in profiles)
        {
            writer.WriteLine($"  <PropertyGroup Condition=\"'$(Configuration)' == '{profile}'\">");
            writer.WriteLine($"    <DefineConstants>ARISEN_PROFILE_{profile.ToUpper()}</DefineConstants>");
            writer.WriteLine($"  </PropertyGroup>");
        }

        writer.WriteLine("  <ItemGroup>");
        string srcRel = PathUtils.GetRelativePath(csprojDir, package.DirectoryPath);
        string globRel = srcRel;
        if (Directory.Exists(Path.Combine(package.DirectoryPath, "Managed")))
        {
            globRel = Path.Combine(srcRel, "Managed");
        }
        writer.WriteLine($"    <Compile Include=\"{globRel}\\**\\*.cs\" Link=\"%(RecursiveDir)%(Filename)%(Extension)\" />");
        
        bool hasAvalonia = package.Manifest.NugetDependencies?.ContainsKey("Avalonia") == true;
        if (hasAvalonia)
        {
            writer.WriteLine($"    <AvaloniaXaml Include=\"{globRel}\\**\\*.axaml\" Link=\"%(RecursiveDir)%(Filename)%(Extension)\" />");
            writer.WriteLine($"    <AvaloniaResource Include=\"{globRel}\\Assets\\**\" Exclude=\"{globRel}\\**\\*.axaml;{globRel}\\**\\*.cs\" Link=\"Assets\\%(RecursiveDir)%(Filename)%(Extension)\" />");
            writer.WriteLine($"    <AvaloniaResource Include=\"{globRel}\\Resources\\**\" Exclude=\"{globRel}\\**\\*.axaml;{globRel}\\**\\*.cs\" Link=\"Resources\\%(RecursiveDir)%(Filename)%(Extension)\" />");
            writer.WriteLine($"    <AvaloniaResource Include=\"{globRel}\\**\\*.png\" Link=\"%(RecursiveDir)%(Filename)%(Extension)\" />");
        }
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        writer.WriteLine("    <Using Include=\"System.Numerics\" />");
        writer.WriteLine("    <Using Include=\"System.Runtime.InteropServices\" />");
        writer.WriteLine("    <Using Include=\"ArisenKernel.Contracts\" />");
        writer.WriteLine("    <Using Include=\"ArisenKernel.Packages\" />");
        writer.WriteLine("    <Using Include=\"ArisenKernel.Lifecycle\" />");
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();
        
        writer.WriteLine("  <ItemGroup>");
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string depRel = PathUtils.GetRelativePath(csprojDir, kernelPath);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
        }
        else
        {
            string dllPath = Path.Combine(engineDir, "ArisenKernel", "bin", "$(Configuration)", "net9.0", "ArisenKernel.dll");
            // Note: In an actual bin-distribute, engineDir might be the root where bin/ is compiled.
            // Using a resilient path reference strategy for the final binary engine.
            writer.WriteLine($"    <Reference Include=\"ArisenKernel\">");
            writer.WriteLine($"      <HintPath>..\\..\\bin\\$(Configuration)\\ArisenKernel.dll</HintPath>");
            writer.WriteLine($"    </Reference>");
        }

        if (package.Manifest.Dependencies != null)
        {
            foreach (var dep in package.Manifest.Dependencies.Keys)
            {
                if (map.TryGetValue(dep, out var depInfo))
                {
                    if (depInfo.Manifest.Type == "native") continue;
                    
                    string depPackageName = Path.GetFileName(depInfo.DirectoryPath);
                    string depProjectName = string.Join(".", depPackageName.Split('.').Select(PathUtils.ToPascalCase));
                    string depRel = $"..\\{depProjectName}\\{depProjectName}.csproj";
                    writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
                }
                else
                {
                    Logger.Warning($"Warning: Dependency '{dep}' for package '{packageName}' not found in graph.");
                }
            }
        }
        
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();

        if (package.Manifest.NugetDependencies != null && package.Manifest.NugetDependencies.Count > 0)
        {
            writer.WriteLine("  <ItemGroup>");
            foreach (var kvp in package.Manifest.NugetDependencies)
            {
                writer.WriteLine($"    <PackageReference Include=\"{kvp.Key}\" Version=\"{kvp.Value}\" />");
            }
            writer.WriteLine("  </ItemGroup>");
            writer.WriteLine();
        }

        // INJECTION PIPELINE: Auto-run ArisenBuildTool inject after compilation
        writer.WriteLine("  <Target Name=\"ArisenPostBuildInjection\" AfterTargets=\"Build\">");
        writer.WriteLine($"    <Exec Command=\"{buildCmd} inject --package &quot;{srcRel}&quot; --assembly &quot;$(TargetPath)&quot;\" />");
        writer.WriteLine("  </Target>");
        
        writer.WriteLine("</Project>");
        
        Logger.Info($"Generated CSProj: {csprojPath}");
    }
}
