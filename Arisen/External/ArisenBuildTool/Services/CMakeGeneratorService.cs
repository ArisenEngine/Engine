using System;
using System.Collections.Generic;
using System.IO;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class CMakeGeneratorService
{
    public static void Generate(string engineDir, string projectsDir, List<PackageInfo> nativePackages, string projectName, ProjectManifest manifest, string profile)
    {
        if (nativePackages.Count == 0)
        {
            Logger.Info("No native packages detected. Skipping CMake generation.");
            return;
        }

        Logger.Info($"Generating CMake tree for {nativePackages.Count} native packages (Profile: {profile})...");
        
        string cmakeTargetDir = Path.Combine(projectsDir, "Native");
        Directory.CreateDirectory(cmakeTargetDir);

        string cmakeListsPath = Path.Combine(cmakeTargetDir, "CMakeLists.txt");
        using var writer = new StreamWriter(cmakeListsPath);
        writer.WriteLine("cmake_minimum_required(VERSION 3.29)");
        
        // Use standard Debug;Release for IDE consistency
        writer.WriteLine("set(CMAKE_CONFIGURATION_TYPES \"Debug;Release\" CACHE STRING \"\" FORCE)");

        writer.WriteLine($"project({projectName}_Native)");
        writer.WriteLine($"set(ARISEN_ENGINE_DIR \"{engineDir.Replace('\\', '/')}\")");
        writer.WriteLine("set(CMAKE_CXX_STANDARD 23)");
        writer.WriteLine("set(CMAKE_CXX_STANDARD_REQUIRED ON)");

        string uProf = profile.ToUpper();
        bool profilerEnabled = string.Equals(profile, "Development", StringComparison.OrdinalIgnoreCase);
        
        // Map Debug and Release to the correct profile macro
        writer.WriteLine($"# Profile-specific definitions for {profile}");
        writer.WriteLine($"add_compile_definitions(ARISEN_PROFILE_{uProf})");
        writer.WriteLine($"set(ARISEN_PROFILER_ENABLED {(profilerEnabled ? "1" : "0")} CACHE BOOL \"Enable Arisen profiler instrumentation\" FORCE)");
        writer.WriteLine("if(ARISEN_PROFILER_ENABLED)");
        writer.WriteLine("    add_compile_definitions(ARISEN_PROFILER_ENABLED=1)");
        writer.WriteLine("else()");
        writer.WriteLine("    add_compile_definitions(ARISEN_PROFILER_ENABLED=0)");
        writer.WriteLine("endif()");
        
        // Ensure Debug and Release are isolated even within the same profile's native folder
        // Paths are relative to Projects/{profile}/Native/ - we need 3 levels up to reach .arisen/
        writer.WriteLine($"set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_DEBUG \"${{CMAKE_SOURCE_DIR}}/../../../bin/{profile}/Debug\")");
        writer.WriteLine($"set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_DEBUG \"${{CMAKE_SOURCE_DIR}}/../../../bin/{profile}/Debug\")");
        writer.WriteLine($"set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_RELEASE \"${{CMAKE_SOURCE_DIR}}/../../../bin/{profile}/Release\")");
        writer.WriteLine($"set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_RELEASE \"${{CMAKE_SOURCE_DIR}}/../../../bin/{profile}/Release\")");

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
