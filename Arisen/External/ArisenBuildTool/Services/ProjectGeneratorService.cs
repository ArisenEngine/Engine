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
        foreach (var package in managedPackages)
        {
            GenerateProjectFile(workspaceDir, projectsDir, engineDir, package, packageMap);
        }
    }

    private static void GenerateProjectFile(string workspaceDir, string projectsDir, string engineDir, PackageInfo package, Dictionary<string, PackageInfo> map)
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
        writer.WriteLine($"    <RootNamespace>ArisenEngine.{projectName.Replace("Com.Arisen.", "")}</RootNamespace>");
        writer.WriteLine("    <OutputPath>Output\\$(Configuration)\\</OutputPath>");
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
        writer.WriteLine("    <Using Include=\"ArisenKernel.Contracts\" />");
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();
        
        writer.WriteLine("  <ItemGroup>");
        // Kernel dependency
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string depRel = PathUtils.GetRelativePath(projectsDir, kernelPath);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
        }

        // Explicit manifest dependencies
        if (package.Manifest.Dependencies != null)
        {
            foreach (var dep in package.Manifest.Dependencies.Keys)
            {
                if (map.TryGetValue($"{dep}_managed", out var depInfo) || map.TryGetValue($"{dep}_native", out depInfo))
                {
                    if (depInfo.Manifest.Type == "native") continue;
                    
                    string depPackageName = Path.GetFileName(depInfo.DirectoryPath);
                    string depProjectName = string.Join(".", depPackageName.Split('.').Select(PathUtils.ToPascalCase));
                    string depRel = $"{depProjectName}.csproj"; // Since all csprojs are generated in the same Projects folder!
                    writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
                }
                else
                {
                    Logger.Warning($"Warning: Topological dependency '{dep}' for package '{packageName}' not found in workspace map.");
                }
            }
        }
        
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine("</Project>");
        
        Logger.Info($"Generated CSProj: {csprojPath}");
    }
}
