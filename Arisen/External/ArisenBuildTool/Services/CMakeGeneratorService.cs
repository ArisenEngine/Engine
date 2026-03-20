using System.Collections.Generic;
using System.IO;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class CMakeGeneratorService
{
    public static void Generate(string engineDir, string projectsDir, List<PackageInfo> nativePackages, string projectName)
    {
        if (nativePackages.Count == 0)
        {
            Logger.Info("No native packages detected. Skipping CMake generation.");
            return;
        }

        Logger.Info($"Generating CMake tree for {nativePackages.Count} native packages...");
        
        string cmakeTargetDir = Path.Combine(projectsDir, "Native");
        Directory.CreateDirectory(cmakeTargetDir);

        string cmakeListsPath = Path.Combine(cmakeTargetDir, "CMakeLists.txt");
        using var writer = new StreamWriter(cmakeListsPath);
        writer.WriteLine("cmake_minimum_required(VERSION 3.29)");
        writer.WriteLine($"project({projectName}_Native)");
        writer.WriteLine("set(CMAKE_CXX_STANDARD 23)");
        writer.WriteLine("set(CMAKE_CXX_STANDARD_REQUIRED ON)");

        string cmakeModulePath = Path.Combine(engineDir, "cmake").Replace('\\', '/'); 
        writer.WriteLine($"list(APPEND CMAKE_MODULE_PATH \"{cmakeModulePath}\")");
        writer.WriteLine("include(Utils)");
        
        writer.WriteLine($"set(CMAKE_RUNTIME_OUTPUT_DIRECTORY \"${{CMAKE_SOURCE_DIR}}/../Output\")");
        writer.WriteLine($"set(CMAKE_LIBRARY_OUTPUT_DIRECTORY \"${{CMAKE_SOURCE_DIR}}/../Output\")");

        foreach(var pkg in nativePackages)
        {
            string relPath = PathUtils.GetRelativePath(cmakeTargetDir, pkg.DirectoryPath);
            writer.WriteLine($"add_subdirectory(\"{relPath.Replace('\\', '/')}\" \"${{CMAKE_CURRENT_BINARY_DIR}}/{Path.GetFileName(pkg.DirectoryPath)}\")");
        }
        writer.Close();

        Logger.Info("Invoking CMake generator natively...");
        string buildDir = Path.Combine(cmakeTargetDir, "build");
        Directory.CreateDirectory(buildDir);
        ProcessRunner.Run("cmake", $"-G \"Visual Studio 17 2022\" -S \"{cmakeTargetDir}\" -B \"{buildDir}\"", cmakeTargetDir);
    }
}
