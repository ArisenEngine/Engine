using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class SolutionGeneratorService
{
    private const string CSHARP_PROJECT_TYPE = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
    private const string VIRTUAL_FOLDER_TYPE = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    public static void Generate(string projectsDir, string engineDir, List<PackageInfo> managedPackages, string projectName, ProjectManifest manifest, string profile)
    {
        string slnPath = Path.Combine(projectsDir, "..", "..", $"{projectName}_{profile}.sln");
        string slnDir = Path.GetDirectoryName(slnPath)!;
        Logger.Info($"Generating {slnPath} purely from C#...");

        // Generate Entry Project
        string entryCsprojDir = Path.Combine(projectsDir, projectName);
        Directory.CreateDirectory(entryCsprojDir);
        string entryCsproj = Path.Combine(entryCsprojDir, $"{projectName}.csproj");
        GenerateEntryPointProject(entryCsproj, engineDir, projectName, manifest, profile);

        // Generate Protective MSVC Property File
        string dirBuildProps = Path.Combine(slnDir, "Directory.Build.props");
        GenerateDirectoryBuildProps(dirBuildProps);

        var configurations = new List<string> { "Debug", "Release" };

        using var writer = new StreamWriter(slnPath);
        writer.WriteLine(); // Start with empty line for Visual Studio encoding expectations
        writer.WriteLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        writer.WriteLine("# Visual Studio Version 17");

        var projectGuids = new Dictionary<string, string>(); // Path -> GUID
        var nestedProjects = new List<(string child, string parent)>();

        // 1. Write Entry Project
        string entryGuid = Guid.NewGuid().ToString("B").ToUpper();
        string entryRel = PathUtils.GetRelativePath(slnDir, entryCsproj);
        writer.WriteLine($"Project(\"{CSHARP_PROJECT_TYPE}\") = \"{projectName}\", \"{entryRel}\", \"{entryGuid}\"");
        writer.WriteLine("EndProject");
        projectGuids[entryGuid] = entryGuid;

        // 2. Write Virtual Folders
        string pkgFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Packages\", \"Packages\", \"{pkgFolderGuid}\"");
        writer.WriteLine("EndProject");

        string localFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Local Packages\", \"Local Packages\", \"{localFolderGuid}\"");
        writer.WriteLine("EndProject");

        string nativeFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Native Dependencies\", \"Native Dependencies\", \"{nativeFolderGuid}\"");
        writer.WriteLine("EndProject");

        // 3. Write Core Kernel
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string kernelRel = PathUtils.GetRelativePath(slnDir, kernelPath);
            string kernelGuid = Guid.NewGuid().ToString("B").ToUpper();
            writer.WriteLine($"Project(\"{CSHARP_PROJECT_TYPE}\") = \"ArisenKernel\", \"{kernelRel}\", \"{kernelGuid}\"");
            writer.WriteLine("EndProject");
            projectGuids[kernelGuid] = kernelGuid;
            nestedProjects.Add((kernelGuid, pkgFolderGuid));
        }

        // 4. Write C# Packages
        foreach (var package in managedPackages)
        {
            string packageName = Path.GetFileName(package.DirectoryPath);
            string pkgProjectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
            string csprojPath = Path.Combine(projectsDir, pkgProjectName, $"{pkgProjectName}.csproj");
            string relPath = PathUtils.GetRelativePath(slnDir, csprojPath);
            
            string guid = Guid.NewGuid().ToString("B").ToUpper();
            projectGuids[guid] = guid;
            
            writer.WriteLine($"Project(\"{CSHARP_PROJECT_TYPE}\") = \"{pkgProjectName}\", \"{relPath}\", \"{guid}\"");
            writer.WriteLine("EndProject");

            string folderGuid = package.DirectoryPath.Contains("ArisenKernel") || package.DirectoryPath.Contains("Local") == false ? pkgFolderGuid : localFolderGuid;
            nestedProjects.Add((guid, folderGuid));
        }

        // 4. Parse & Write Native Projects from CMake .sln
        var nativeProjectGuids = new List<string>();
        string cmakeSln = Path.Combine(projectsDir, "Native", "build", $"{projectName}_Native.sln");
        if (File.Exists(cmakeSln))
        {
            string[] lines = File.ReadAllLines(cmakeSln);
            
            HashSet<string> skippedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in lines)
            {
                var m = Regex.Match(l, @"Project\(""(.*?)""\)\s*=\s*""(.*?)"",\s*""(.*?)"",\s*""(\{.*?\})""");
                if (m.Success)
                {
                    string pN = m.Groups[2].Value;
                    if (pN == "CMakePredefinedTargets" || pN == "ALL_BUILD" || pN == "INSTALL")
                    {
                        skippedGuids.Add(m.Groups[4].Value);
                    }
                }
            }
            
            bool insideProject = false;
            bool skipProject = false;
            string currentProjGuid = "";

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Project("))
                {
                    insideProject = true;
                    var match = Regex.Match(line, @"Project\(""(.*?)""\)\s*=\s*""(.*?)"",\s*""(.*?)"",\s*""(.*?)""");
                    if (match.Success)
                    {
                        string pType = match.Groups[1].Value;
                        string pName = match.Groups[2].Value;
                        string pPath = match.Groups[3].Value;
                        currentProjGuid = match.Groups[4].Value;

                        if (pName == "CMakePredefinedTargets" || pName == "ALL_BUILD" || pName == "INSTALL") 
                        {
                            skipProject = true;
                            continue;
                        }

                        skipProject = false;
                        
                        if (pType == VIRTUAL_FOLDER_TYPE.Trim('{', '}'))
                        {
                            writer.WriteLine(line);
                        }
                        else
                        {
                            nativeProjectGuids.Add(currentProjGuid);
                            string absoluteVcxproj = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cmakeSln)!, pPath));
                            string mergedRelPath = PathUtils.GetRelativePath(slnDir, absoluteVcxproj);
                            writer.WriteLine($"Project(\"{pType}\") = \"{pName}\", \"{mergedRelPath}\", \"{currentProjGuid}\"");
                        }
                        nestedProjects.Add((currentProjGuid, nativeFolderGuid));
                    }
                }
                else if (line.StartsWith("EndProject"))
                {
                    if (!skipProject) writer.WriteLine(line);
                    insideProject = false;
                    skipProject = false;
                }
                else if (insideProject && !skipProject)
                {
                    bool isBanned = false;
                    foreach(var g in skippedGuids)
                    {
                        if (line.Contains(g))
                        {
                            isBanned = true;
                            break;
                        }
                    }
                    if (!isBanned) writer.WriteLine(line);
                }
            }
        }

        // Write Global section
        writer.WriteLine("Global");
        
        // Profiles Map
        writer.WriteLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        foreach(var config in configurations)
        {
            writer.WriteLine($"\t\t{config}|x64 = {config}|x64");
        }
        writer.WriteLine("\tEndGlobalSection");
 
        // Project Maps
        writer.WriteLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var guid in projectGuids.Keys) // C# Projects
        {
            foreach (var config in configurations)
            {
                writer.WriteLine($"\t\t{guid}.{config}|x64.ActiveCfg = {config}|Any CPU");
                writer.WriteLine($"\t\t{guid}.{config}|x64.Build.0 = {config}|Any CPU");
            }
        }
        foreach (var guid in nativeProjectGuids) // C++ Projects
        {
            foreach (var config in configurations)
            {
                writer.WriteLine($"\t\t{guid}.{config}|x64.ActiveCfg = {config}|x64");
                writer.WriteLine($"\t\t{guid}.{config}|x64.Build.0 = {config}|x64");
            }
        }
        writer.WriteLine("\tEndGlobalSection");

        // Folders
        writer.WriteLine("\tGlobalSection(NestedProjects) = preSolution");
        foreach (var tuple in nestedProjects)
        {
            writer.WriteLine($"\t\t{tuple.child} = {tuple.parent}");
        }
        writer.WriteLine("\tEndGlobalSection");
        
        writer.WriteLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
        writer.WriteLine($"\t\tSolutionGuid = {Guid.NewGuid().ToString("B").ToUpper()}");
        writer.WriteLine("\tEndGlobalSection");

        writer.WriteLine("EndGlobal");
    }

    private static void GenerateEntryPointProject(string csprojPath, string engineDir, string projectName, ProjectManifest manifest, string profile)
    {
        using StreamWriter writer = new StreamWriter(csprojPath);
        writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        writer.WriteLine("  <PropertyGroup>");
        writer.WriteLine("    <OutputType>Exe</OutputType>");
        writer.WriteLine("    <TargetFramework>net9.0</TargetFramework>");
        writer.WriteLine("    <ImplicitUsings>enable</ImplicitUsings>");
        writer.WriteLine("    <Nullable>enable</Nullable>");
        // Output binaries mapped isolated
        writer.WriteLine($"    <OutputPath>..\\..\\..\\bin\\{profile}\\$(Configuration)\\</OutputPath>");
        writer.WriteLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        writer.WriteLine("    <PlatformTarget>x64</PlatformTarget>");
        writer.WriteLine($"    <RootNamespace>{projectName}</RootNamespace>");
        writer.WriteLine("    <RuntimeIdentifier>win-x64</RuntimeIdentifier>");
        writer.WriteLine("    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>");
        writer.WriteLine("    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>");
        writer.WriteLine($"    <DefineConstants>ARISEN_PROFILE_{profile.ToUpper()}</DefineConstants>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\">");
        writer.WriteLine("    <Optimize>false</Optimize>");
        writer.WriteLine("    <DebugSymbols>true</DebugSymbols>");
        writer.WriteLine("  </PropertyGroup>");

        writer.WriteLine("  <PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">");
        writer.WriteLine("    <Optimize>true</Optimize>");
        writer.WriteLine("    <DebugSymbols>false</DebugSymbols>");
        writer.WriteLine("  </PropertyGroup>");

        writer.WriteLine("  <ItemGroup>");
        string kernelSource = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelSource))
        {
            string depRel = PathUtils.GetRelativePath(Path.GetDirectoryName(csprojPath)!, kernelSource);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
        }
        else
        {
             writer.WriteLine("    <Reference Include=\"ArisenKernel\">");
             writer.WriteLine($"      <HintPath>..\\..\\..\\bin\\{profile}\\$(Configuration)\\ArisenKernel.dll</HintPath>");
             writer.WriteLine("    </Reference>");
        }
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine("</Project>");
        
        // Generate a thin Program.cs Stub
        string entryPointSource = @"using System;
namespace {0};
public class Program {{
    public static void Main(string[] args) => ArisenKernel.Lifecycle.EngineBootstrapper.Run(args);
}}";
        string programPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "Program.cs");
        File.WriteAllText(programPath, string.Format(entryPointSource, projectName));
    }

    private static void GenerateDirectoryBuildProps(string propsPath)
    {
        using StreamWriter writer = new StreamWriter(propsPath);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        writer.WriteLine("<Project>");
        writer.WriteLine("  <!-- Protective override for unmapped C++ evaluation folders -->");
        writer.WriteLine("  <PropertyGroup Condition=\"'$(MSBuildProjectExtension)' == '.vcxproj'\">");
        writer.WriteLine("    <OutDir>$(SolutionDir)Projects\\Native\\build\\$(Platform)\\$(Configuration)\\</OutDir>");
        writer.WriteLine("    <IntDir>$(SolutionDir)Projects\\Native\\build\\$(Platform)\\$(Configuration)\\$(ProjectName)\\</IntDir>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine("</Project>");
    }
}
