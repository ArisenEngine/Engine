using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class SolutionGeneratorService
{
    public static void Generate(string projectsDir, string engineDir, Dictionary<string, PackageInfo> packageMap, string projectName)
    {
        string slnPath = Path.Combine(projectsDir, "..", $"{projectName}.sln"); // Root of workspace
        string slnDir = Path.GetDirectoryName(slnPath)!;
        Logger.Info($"Generating {slnPath}...");
        
        ProcessRunner.Run("dotnet", $"new sln -n {projectName} --force", slnDir);
        
        // Generate the Entry Point executable project (.arisen/Projects/projectName.csproj)
        string entryCsproj = Path.Combine(projectsDir, $"{projectName}.csproj");
        GenerateEntryPointProject(entryCsproj, engineDir, projectName);
        
        string entryCsprojRel = PathUtils.GetRelativePath(slnDir, entryCsproj);
        ProcessRunner.Run("dotnet", $"sln {projectName}.sln add \"{entryCsprojRel}\"", slnDir);

        // Add auto-generated C# Packages
        foreach (var package in packageMap.Values.Where(p => p.Manifest.Type != "native"))
        {
            string packageName = Path.GetFileName(package.DirectoryPath);
            string pkgProjectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
            string csprojPath = Path.Combine(projectsDir, $"{pkgProjectName}.csproj");
            
            string relPath = PathUtils.GetRelativePath(slnDir, csprojPath);
            string virtualFolder = package.DirectoryPath.Contains("ArisenKernel") || package.DirectoryPath.Contains("Local") == false ? "Packages" : "Local Packages";
            
            ProcessRunner.Run("dotnet", $"sln {projectName}.sln add --solution-folder \"{virtualFolder}\" \"{relPath}\"", slnDir);
        }

        // Add Native C++ Packages (Assuming CMake generates inside Projects/Native/)
        if (packageMap.Values.Any(p => p.Manifest.Type == "native"))
        {
            string buildDir = Path.Combine(projectsDir, "Native", "build");
            if (Directory.Exists(buildDir))
            {
                string[] vcxprojs = Directory.GetFiles(buildDir, "*.vcxproj", SearchOption.AllDirectories);
                foreach (var vcxproj in vcxprojs)
                {
                    if (vcxproj.Contains("ZERO_CHECK") || vcxproj.Contains("ALL_BUILD") || vcxproj.Contains("CompilerId")) continue;
                    string relPath = PathUtils.GetRelativePath(slnDir, vcxproj);
                    ProcessRunner.Run("dotnet", $"sln {projectName}.sln add --solution-folder \"Native Dependencies\" \"{relPath}\"", slnDir);
                }
            }
        }
    }

    private static void GenerateEntryPointProject(string csprojPath, string engineDir, string projectName)
    {
        using StreamWriter writer = new StreamWriter(csprojPath);
        writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        writer.WriteLine("  <PropertyGroup>");
        writer.WriteLine("    <OutputType>Exe</OutputType>");
        writer.WriteLine("    <TargetFramework>net9.0</TargetFramework>");
        writer.WriteLine("    <ImplicitUsings>enable</ImplicitUsings>");
        writer.WriteLine("    <Nullable>enable</Nullable>");
        // Output locally into .arisen/bin/$(Configuration)/
        writer.WriteLine("    <OutputPath>..\\bin\\$(Configuration)\\</OutputPath>");
        writer.WriteLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        writer.WriteLine("    <PlatformTarget>x64</PlatformTarget>");
        writer.WriteLine($"    <RootNamespace>{projectName}</RootNamespace>");
        writer.WriteLine("    <RuntimeIdentifier>win-x64</RuntimeIdentifier>");
        writer.WriteLine("    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        string hostSrcDir = Path.Combine(engineDir, "ArisenHost");
        if (Directory.Exists(hostSrcDir))
        {
            string srcRel = PathUtils.GetRelativePath(Path.GetDirectoryName(csprojPath)!, hostSrcDir);
            writer.WriteLine($"    <Compile Include=\"{srcRel}\\**\\*.cs\" />");
        }
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string depRel = PathUtils.GetRelativePath(Path.GetDirectoryName(csprojPath)!, kernelPath);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
        }
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine("</Project>");
    }
}
