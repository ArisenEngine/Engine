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

    public static void Generate(string projectsDir, string engineDir, List<PackageInfo> managedPackages, string projectName, ProjectManifest manifest, string profile, bool isEditor)
    {
        string slnPath = Path.Combine(projectsDir, "..", "..", $"{projectName}_{profile}.sln");
        string slnDir = Path.GetDirectoryName(slnPath)!;
        Logger.Info($"Generating {slnPath} purely from C#...");

        // Generate Entry Project
        string entryCsprojDir = Path.Combine(projectsDir, projectName);
        Directory.CreateDirectory(entryCsprojDir);
        string entryCsproj = Path.Combine(entryCsprojDir, $"{projectName}.csproj");
        GenerateEntryPointProject(entryCsproj, engineDir, projectName, manifest, profile, isEditor, managedPackages, projectsDir);

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
        
        // Track folder usage
        string pkgFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        string localFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        string nativeFolderGuid = Guid.NewGuid().ToString("B").ToUpper();
        bool hasPkgFolder = false;
        bool hasLocalFolder = false;
        bool hasNativeFolder = false;

        var slnProjects = new List<string>(); // Buffered project lines

        // 1. Write Entry Project
        string entryGuid = Guid.NewGuid().ToString("B").ToUpper();
        string entryRel = PathUtils.GetRelativePath(slnDir, entryCsproj);
        projectGuids[entryGuid] = entryGuid;

        // 2. Write Core Kernel
        string kernelPath = Path.Combine(engineDir, "ArisenKernel", "ArisenKernel.csproj");
        if (File.Exists(kernelPath))
        {
            string kernelRel = PathUtils.GetRelativePath(slnDir, kernelPath);
            string kernelGuid = Guid.NewGuid().ToString("B").ToUpper();
            slnProjects.Add($"Project(\"{CSHARP_PROJECT_TYPE}\") = \"ArisenKernel\", \"{kernelRel}\", \"{kernelGuid}\"");
            slnProjects.Add("EndProject");
            projectGuids[kernelGuid] = kernelGuid;
            // ArisenKernel is now at root - no nesting
        }

        // 3. Write C# Packages
        foreach (var package in managedPackages)
        {
            string packageName = Path.GetFileName(package.DirectoryPath);
            string pkgProjectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
            string csprojPath = Path.Combine(projectsDir, pkgProjectName, $"{pkgProjectName}.csproj");
            string relPath = PathUtils.GetRelativePath(slnDir, csprojPath);
            
            string guid = Guid.NewGuid().ToString("B").ToUpper();
            projectGuids[guid] = guid;
            
            slnProjects.Add($"Project(\"{CSHARP_PROJECT_TYPE}\") = \"{pkgProjectName}\", \"{relPath}\", \"{guid}\"");
            slnProjects.Add("EndProject");

            bool isLocal = package.DirectoryPath.Contains("Local");
            string folderGuid = isLocal ? localFolderGuid : pkgFolderGuid;
            
            if (isLocal) hasLocalFolder = true;
            else hasPkgFolder = true;

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

                        // Skip VIRTUAL_FOLDER_TYPE from native solution to avoid "nothing" folders like 3rdparty, Core, RHI
                        if (string.Equals(pType, VIRTUAL_FOLDER_TYPE, StringComparison.OrdinalIgnoreCase))
                        {
                            skipProject = true;
                            continue;
                        }

                        skipProject = false;
                        hasNativeFolder = true;
                        nativeProjectGuids.Add(currentProjGuid);

                        string absoluteVcxproj = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cmakeSln)!, pPath));
                        string mergedRelPath = PathUtils.GetRelativePath(slnDir, absoluteVcxproj);
                        slnProjects.Add($"Project(\"{pType}\") = \"{pName}\", \"{mergedRelPath}\", \"{currentProjGuid}\"");
                        nestedProjects.Add((currentProjGuid, nativeFolderGuid));
                    }
                }
                else if (line.StartsWith("EndProject"))
                {
                    if (!skipProject) slnProjects.Add(line);
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
                    if (!isBanned) slnProjects.Add(line);
                }
            }
        }

        // 5. Add Entry Project with dependencies on native projects to the start of the solution
        // We do this here after all nativeProjectGuids have been identified
        string entryProjHeader = $"Project(\"{CSHARP_PROJECT_TYPE}\") = \"{projectName}\", \"{entryRel}\", \"{entryGuid}\"";
        if (nativeProjectGuids.Count > 0)
        {
            var entryProjLines = new List<string> { entryProjHeader };
            entryProjLines.Add("\tProjectSection(ProjectDependencies) = postProject");
            foreach (var nGuid in nativeProjectGuids)
            {
                entryProjLines.Add($"\t\t{nGuid} = {nGuid}");
            }
            entryProjLines.Add("\tEndProjectSection");
            entryProjLines.Add("EndProject");
            slnProjects.InsertRange(0, entryProjLines);
        }
        else
        {
            slnProjects.Insert(0, entryProjHeader);
            slnProjects.Insert(1, "EndProject");
        }

        // Now write everything to the solution file
        // 1. Folders (Dynamic)
        if (hasPkgFolder)
        {
            writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Packages\", \"Packages\", \"{pkgFolderGuid}\"");
            writer.WriteLine("EndProject");
        }
        if (hasLocalFolder)
        {
            writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Local Packages\", \"Local Packages\", \"{localFolderGuid}\"");
            writer.WriteLine("EndProject");
        }
        if (hasNativeFolder)
        {
            writer.WriteLine($"Project(\"{VIRTUAL_FOLDER_TYPE}\") = \"Native Dependencies\", \"Native Dependencies\", \"{nativeFolderGuid}\"");
            writer.WriteLine("EndProject");
        }

        // 2. Projects
        foreach (var projLine in slnProjects)
        {
            writer.WriteLine(projLine);
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
        foreach (var guid in nativeProjectGuids) // C++ Projects (Ensure Build.0 is enabled for MSBuild/VS)
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
            // Only write nesting if the parent folder was actually created
            bool parentExists = (tuple.parent == pkgFolderGuid && hasPkgFolder) ||
                                (tuple.parent == localFolderGuid && hasLocalFolder) ||
                                (tuple.parent == nativeFolderGuid && hasNativeFolder);
                                
            if (parentExists)
            {
                writer.WriteLine($"\t\t{tuple.child} = {tuple.parent}");
            }
        }
        writer.WriteLine("\tEndGlobalSection");
        
        writer.WriteLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
        writer.WriteLine($"\t\tSolutionGuid = {Guid.NewGuid().ToString("B").ToUpper()}");
        writer.WriteLine("\tEndGlobalSection");

        writer.WriteLine("EndGlobal");
    }

    private static void GenerateEntryPointProject(string csprojPath, string engineDir, string projectName, ProjectManifest manifest, string profile, bool isEditor, List<PackageInfo> managedPackages, string projectsDir)
    {
        if (TryGenerateLauncherHostProject(csprojPath, engineDir, projectName, profile, isEditor))
        {
            return;
        }

        string csprojDir = Path.GetDirectoryName(csprojPath)!;

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
        
        string constants = $"ARISEN_PROFILE_{profile.ToUpper()}";
        if (isEditor) constants += ";ARISEN_ENGINE_EDITOR";
        writer.WriteLine($"    <DefineConstants>{constants}</DefineConstants>");
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
            string depRel = PathUtils.GetRelativePath(csprojDir, kernelSource);
            writer.WriteLine($"    <ProjectReference Include=\"{depRel}\" />");
        }
        else
        {
             writer.WriteLine("    <Reference Include=\"ArisenKernel\">");
             writer.WriteLine($"      <HintPath>..\\..\\..\\bin\\{profile}\\$(Configuration)\\ArisenKernel.dll</HintPath>");
             writer.WriteLine("    </Reference>");
        }

        // Add Project References to all discovered managed packages to ensure IDE recompilation on "Run"
        // We set ReferenceOutputAssembly to false because they are loaded dynamically at runtime.
        foreach (var package in managedPackages)
        {
            string packageName = Path.GetFileName(package.DirectoryPath);
            string pkgProjectName = string.Join(".", packageName.Split('.').Select(PathUtils.ToPascalCase));
            string pkgCsprojPath = Path.Combine(projectsDir, pkgProjectName, $"{pkgProjectName}.csproj");
            string relPath = PathUtils.GetRelativePath(csprojDir, pkgCsprojPath);
            
            writer.WriteLine($"    <ProjectReference Include=\"{relPath}\" ReferenceOutputAssembly=\"false\" SkipGetTargetFrameworkProperties=\"true\" />");
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

    private static bool TryGenerateLauncherHostProject(string csprojPath, string engineDir, string projectName, string profile, bool isEditor)
    {
        if (!string.Equals(projectName, "ArisenLauncher", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string desktopProject = Path.Combine(engineDir, "Editor", "ArisenLauncher.Desktop", "ArisenLauncher.Desktop.csproj");
        string launcherProject = Path.Combine(engineDir, "Editor", "ArisenLauncher", "ArisenLauncher.csproj");
        if (!File.Exists(desktopProject))
        {
            return false;
        }

        string csprojDir = Path.GetDirectoryName(csprojPath)!;
        string desktopProjectRel = PathUtils.GetRelativePath(csprojDir, desktopProject);
        string launcherProjectRel = PathUtils.GetRelativePath(csprojDir, launcherProject);

        using StreamWriter writer = new StreamWriter(csprojPath);
        writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        writer.WriteLine("  <PropertyGroup>");
        writer.WriteLine("    <OutputType>WinExe</OutputType>");
        writer.WriteLine("    <TargetFramework>net9.0</TargetFramework>");
        writer.WriteLine("    <ImplicitUsings>enable</ImplicitUsings>");
        writer.WriteLine("    <Nullable>enable</Nullable>");
        writer.WriteLine($"    <OutputPath>..\\..\\..\\bin\\{profile}\\$(Configuration)\\</OutputPath>");
        writer.WriteLine("    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
        writer.WriteLine("    <PlatformTarget>x64</PlatformTarget>");
        writer.WriteLine("    <AssemblyName>ArisenLauncher.Host</AssemblyName>");
        writer.WriteLine("    <RootNamespace>ArisenLauncher.Host</RootNamespace>");
        writer.WriteLine("    <RuntimeIdentifier>win-x64</RuntimeIdentifier>");
        writer.WriteLine("    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>");
        writer.WriteLine("    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>");

        string constants = $"ARISEN_PROFILE_{profile.ToUpperInvariant()}";
        if (isEditor) constants += ";ARISEN_ENGINE_EDITOR";
        writer.WriteLine($"    <DefineConstants>{constants}</DefineConstants>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\">");
        writer.WriteLine("    <Optimize>false</Optimize>");
        writer.WriteLine("    <DebugSymbols>true</DebugSymbols>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">");
        writer.WriteLine("    <Optimize>true</Optimize>");
        writer.WriteLine("    <DebugSymbols>false</DebugSymbols>");
        writer.WriteLine("  </PropertyGroup>");
        writer.WriteLine();

        writer.WriteLine("  <ItemGroup>");
        if (File.Exists(launcherProject))
        {
            writer.WriteLine($"    <ProjectReference Include=\"{launcherProjectRel}\" />");
        }
        writer.WriteLine($"    <ProjectReference Include=\"{desktopProjectRel}\" />");
        writer.WriteLine("  </ItemGroup>");
        writer.WriteLine();

        writer.WriteLine("  <Target Name=\"CopyStableLauncherAppHost\" AfterTargets=\"Build\">");
        writer.WriteLine("    <Copy SourceFiles=\"$(TargetDir)ArisenLauncher.Desktop.exe\" DestinationFiles=\"$(TargetDir)$(AssemblyName).exe\" SkipUnchangedFiles=\"false\" Condition=\"Exists('$(TargetDir)ArisenLauncher.Desktop.exe')\" />");
        writer.WriteLine("    <Copy SourceFiles=\"$(TargetDir)ArisenLauncher.Desktop.exe\" DestinationFiles=\"$(TargetDir)ArisenLauncher.exe\" SkipUnchangedFiles=\"false\" Condition=\"Exists('$(TargetDir)ArisenLauncher.Desktop.exe')\" />");
        writer.WriteLine("  </Target>");
        writer.WriteLine("</Project>");

        string entryPointSource = @"using System;

namespace ArisenLauncher.Host;
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => ArisenLauncher.Desktop.Program.Main(args);
}";
        string programPath = Path.Combine(csprojDir, "Program.cs");
        File.WriteAllText(programPath, entryPointSource);

        Logger.Info("Generated launcher desktop host entry project for Rider/debugger startup.");
        return true;
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
