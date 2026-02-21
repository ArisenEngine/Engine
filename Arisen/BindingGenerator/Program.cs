using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CppSharp;
using BindingGenerator.Modules;

namespace BindingGenerator;

internal static class Program
{
    static void Main(string[] args)
    {
        ParseArguments(args);

        if (string.IsNullOrEmpty(GlobalConfig.s_SourceCode) || string.IsNullOrEmpty(GlobalConfig.s_Output))
        {
            Console.WriteLine("Missing required arguments: --source_code and --output");
            return;
        }

        var finalOutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName);
        if (Directory.Exists(finalOutputDir))
        {
            DeleteDirectory(finalOutputDir);
        }

        Console.WriteLine("Generating unified bindings...");
        ConsoleDriver.Run(new ArisenUnifiedLibrary());

        Console.WriteLine("Patching DllImport names...");
        PatchDllImports(finalOutputDir);

        Console.WriteLine("Generating AutoBinding.csproj...");
        GenerateProjectFile(finalOutputDir);
    }

    static void GenerateProjectFile(string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var projPath = Path.Combine(outputDir, "AutoBinding.csproj");
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <BaseOutputPath>..\..\x64\</BaseOutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <None Include=""AutoBinding.csproj"" />
    <Reference Include=""CppSharp.Runtime"">
      <HintPath>..\..\3rdparty\CppSharp\CppSharp.Runtime.dll</HintPath>
      <Private>true</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Compile Remove=""ArisenNative-symbols.cpp"" />
    <Compile Remove=""Std-symbols.cpp"" />
  </ItemGroup>

</Project>
";
        File.WriteAllText(projPath, content);
    }

    static void PatchDllImports(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return;

        var files = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(rootDir, file).Replace("\\", "/");

            string dllName = "Core.Foundation.dll"; // Default

            var fileName = Path.GetFileName(file);
            if (fileName == "Logger.cs" || fileName == "Log.cs") 
                dllName = "Core.Diagnostic.dll";
            else if (fileName == "EngineInit.cs" || fileName == "RenderWindowAPI.h" || fileName == "RenderWindowAPI.cs") 
                dllName = "Core.HAL.dll";
            else if (fileName == "RHIHandle.cs") 
                dllName = "Core.RHI.dll";
            else if (fileName == "ShaderCompilerAPI.cs") 
                dllName = "Core.ShaderCompiler.dll";
            else if (fileName == "Assertion.cs" || fileName == "ILogHandler.cs" || fileName == "String.cs" || fileName == "Std.cs")
                dllName = "Core.Foundation.dll";

            // Replace DllImport("ArisenNative" ...) with the correct DLL name
            var patchedContent = content.Replace("DllImport(\"ArisenNative\"", $"DllImport(\"{dllName}\"");
            
            if (content != patchedContent)
            {
                File.WriteAllText(file, patchedContent);
                Console.WriteLine($"Patched {relativePath} -> {dllName}");
            }
        }
    }

    static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source_code" && i + 1 < args.Length)
                GlobalConfig.s_SourceCode = args[i + 1];
            else if (args[i] == "--output" && i + 1 < args.Length)
                GlobalConfig.s_Output = args[i + 1];
            else if (args[i] == "--library" && i + 1 < args.Length)
                GlobalConfig.s_LibraryPath = args[i + 1];
        }
    }

    static void DeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.GetDirectories(directoryPath))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName == "bin" || dirName == "obj")
                continue;

            Directory.Delete(dir, true);
        }
    }
}