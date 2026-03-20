using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class ProjectGeneratorService
{
    public static void GenerateForManagedPackages(string workspaceDir, string projectsDir, string engineDir, List<PackageInfo> managedPackages, Dictionary<string, PackageInfo> packageMap)
    {
        string buildExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string buildCmd = buildExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
            ? $"&quot;{buildExePath}&quot;" 
            : $"dotnet &quot;{buildExePath}&quot;";

        foreach (var package in managedPackages)
        {
            GenerateProjectFile(workspaceDir, projectsDir, engineDir, package, packageMap, buildCmd);
        }
    }

    private static void GenerateProjectFile(string workspaceDir, string projectsDir, string engineDir, PackageInfo package, Dictionary<string, PackageInfo> map, string buildCmd)
    {
        string packageName = Path.GetFileName(package.DirectoryPath);
        string projectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
        string csprojPath = Path.Combine(projectsDir, $"{projectName}.csproj");

        using StreamWriter writer = new StreamWriter(csprojPath);
        writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        writer.WriteLine("  <PropertyGroup>");
        writer.WriteLine("    <TargetFramework>net9.0</TargetFramework>");
        writer.WriteLine("    <ImplicitUsings>enable</ImplicitUsings>");
        writer.WriteLine("    <Nullable>enable</Nullable>");
        writer.WriteLine("    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>");
        writer.WriteLine($"    <RootNamespace>ArisenEngine.{projectName.Replace("Com.Arisen.", "").Replace("Com.User.", "")}</RootNamespace>");
        
        // Output binaries mapped uniformly into MyGame/.arisen/bin/
        writer.WriteLine("    <OutputPath>..\\bin\\$(Configuration)\\</OutputPath>");
        writer.WriteLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        string srcRel = PathUtils.GetRelativePath(projectsDir, package.DirectoryPath);
        writer.WriteLine($"    <Compile Include=\"{srcRel}\\**\\*.cs\" />");
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        writer.WriteLine("    <Using Include=\"System.Numerics\" />");
        writer.WriteLine("    <Using Include=\"System.Runtime.InteropServices\" />");
        writer.WriteLine("    <Using Include=\"ArisenKernel.Contracts\" />");
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();
        
        writer.WriteLine("  <ItemGroup>");
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string depRel = PathUtils.GetRelativePath(projectsDir, kernelPath);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
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
                    string depRel = $"{depProjectName}.csproj";
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

        // INJECTION PIPELINE: Auto-run ArisenBuildTool inject after compilation
        writer.WriteLine("  <Target Name=\"ArisenPostBuildInjection\" AfterTargets=\"Build\">");
        writer.WriteLine($"    <Exec Command=\"{buildCmd} inject --package &quot;{srcRel}&quot; --assembly &quot;$(TargetPath)&quot;\" />");
        writer.WriteLine("  </Target>");
        
        writer.WriteLine("</Project>");
        
        Logger.Info($"Generated CSProj: {csprojPath}");
    }
}
