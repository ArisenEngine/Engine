using System.Collections.Generic;
using System.IO;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class CMakeGeneratorService
{
    public static void Generate(string engineDir, string projectsDir, List<PackageInfo> nativePackages, string projectName, ProjectManifest manifest)
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
        writer.WriteLine($"set(ARISEN_ENGINE_DIR \"{engineDir.Replace('\\', '/')}\")");
        writer.WriteLine("set(CMAKE_CXX_STANDARD 23)");
        writer.WriteLine("set(CMAKE_CXX_STANDARD_REQUIRED ON)");

        string[] profiles = manifest.Profiles != null && manifest.Profiles.Count > 0 
            ? manifest.Profiles.Keys.ToArray() 
            : new[] { "Development", "Production" };
            
        string configTypes = string.Join(";", profiles);
        writer.WriteLine($"set(CMAKE_CONFIGURATION_TYPES \"{configTypes}\" CACHE STRING \"\" FORCE)");

        string cmakeModulePath = Path.Combine(engineDir, "cmake").Replace('\\', '/'); 
        writer.WriteLine($"list(APPEND CMAKE_MODULE_PATH \"{cmakeModulePath}\")");
        writer.WriteLine("include(Utils)");
        
        writer.WriteLine("option(ARISEN_ENABLE_PROFILER \"Enable Tracy Profiler\" ON)");
        writer.WriteLine("if(ARISEN_ENABLE_PROFILER)");
        writer.WriteLine("    set(ARISEN_PROFILER_ENABLED ON)");
        writer.WriteLine("    set(TRACY_ENABLE ON CACHE BOOL \"\" FORCE)");
        writer.WriteLine("    set(TRACY_STATIC OFF CACHE BOOL \"\" FORCE)");
        writer.WriteLine("    set(BUILD_SHARED_LIBS ON CACHE BOOL \"\" FORCE)");
        writer.WriteLine("    add_subdirectory(\"${ARISEN_ENGINE_DIR}/3rdparty/tracy\" \"${CMAKE_CURRENT_BINARY_DIR}/3rdparty/tracy\")");
        writer.WriteLine("    set_target_properties(TracyClient PROPERTIES FOLDER \"3rdparty\")");
        writer.WriteLine("    add_compile_definitions(ARISEN_PROFILER_ENABLED=1)");
        writer.WriteLine("else()");
        writer.WriteLine("    set(ARISEN_PROFILER_ENABLED OFF)");
        writer.WriteLine("    add_compile_definitions(ARISEN_PROFILER_ENABLED=0)");
        writer.WriteLine("endif()");
        
        writer.WriteLine("add_subdirectory(\"${ARISEN_ENGINE_DIR}/3rdparty/spdlog\" \"${CMAKE_CURRENT_BINARY_DIR}/3rdparty/spdlog\")");
        writer.WriteLine("set_target_properties(spdlog PROPERTIES FOLDER \"3rdparty\")");
        
        foreach (var profile in profiles)
        {
            string uProf = profile.ToUpper();
            
            // Initialize required CMake variables for non-standard configuration types linking back to RelWithDebInfo
            writer.WriteLine($"set(CMAKE_C_FLAGS_{uProf} \"${{CMAKE_C_FLAGS_RELWITHDEBINFO}}\")");
            writer.WriteLine($"set(CMAKE_CXX_FLAGS_{uProf} \"${{CMAKE_CXX_FLAGS_RELWITHDEBINFO}}\")");
            writer.WriteLine($"set(CMAKE_EXE_LINKER_FLAGS_{uProf} \"${{CMAKE_EXE_LINKER_FLAGS_RELWITHDEBINFO}}\")");
            writer.WriteLine($"set(CMAKE_SHARED_LINKER_FLAGS_{uProf} \"${{CMAKE_SHARED_LINKER_FLAGS_RELWITHDEBINFO}}\")");
            writer.WriteLine($"set(CMAKE_STATIC_LINKER_FLAGS_{uProf} \"${{CMAKE_STATIC_LINKER_FLAGS_RELWITHDEBINFO}}\")");
            writer.WriteLine($"set(CMAKE_MODULE_LINKER_FLAGS_{uProf} \"${{CMAKE_MODULE_LINKER_FLAGS_RELWITHDEBINFO}}\")");
            
            writer.WriteLine($"set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_{uProf} \"${{CMAKE_SOURCE_DIR}}/../../bin/{profile}\")");
            writer.WriteLine($"set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_{uProf} \"${{CMAKE_SOURCE_DIR}}/../../bin/{profile}\")");
            writer.WriteLine($"add_compile_definitions($<$<CONFIG:{profile}>:ARISEN_PROFILE_{uProf}>)");
        }

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
