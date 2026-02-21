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

        Console.WriteLine("Post-processing generated code...");
        PostProcessGeneratedCode(finalOutputDir);

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

    static void PostProcessGeneratedCode(string rootDir)
    {
        if (!Directory.Exists(rootDir)) return;

        var files = Directory.GetFiles(rootDir, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(rootDir, file).Replace("\\", "/");

            var fileName = Path.GetFileName(file);
            string dllName = "Core.Foundation.dll"; // Default

            if (fileName == "Logger.cs" || fileName == "Log.cs") 
                dllName = "Core.Diagnostic.dll";
            else if (fileName.Contains("RenderWindowAPI") || fileName == "EngineInit.cs") 
                dllName = "Core.HAL.dll";
            else if (fileName == "RHIHandle.cs" || fileName == "RHIDevice.cs" || fileName == "RHILoader.cs" || fileName == "RHIInstance.cs") 
                dllName = "Core.RHI.dll";
            else if (fileName == "ShaderCompilerAPI.cs") 
                dllName = "Core.ShaderCompiler.dll";

            var patchedContent = content;

            // 1. Patch DllImport names
            patchedContent = patchedContent.Replace("DllImport(\"ArisenNative\"", $"DllImport(\"{dllName}\"");

            // 2. Hide __Internal structures (make them internal instead of public)
            patchedContent = patchedContent.Replace("public partial struct __Internal", "internal partial struct __Internal");

            // 3. Hide CppSharp noise from IntelliSense
            patchedContent = patchedContent.Replace("public __IntPtr __Instance", "internal __IntPtr __Instance");
            patchedContent = patchedContent.Replace("public void Dispose()", "internal void Dispose()");

            // 5. Remove redundant "Tag" classes (they are just markers and add 1000s of lines of noise)
            patchedContent = Regex.Replace(patchedContent, 
                @"    public unsafe partial class \w+Tag : IDisposable\s+\{.*?internal protected virtual void Dispose\(bool disposing, bool callNativeDtor \)\s+\{.*?\}\s+\}", 
                "", RegexOptions.Singleline);

            // 6. Cleanup the deep namespace prefixes
            patchedContent = patchedContent.Replace("global::ArisenBinding.ArisenEngine.", "ArisenBinding.");
            patchedContent = patchedContent.Replace("ArisenBinding.ArisenEngine.", "ArisenBinding.");
            patchedContent = patchedContent.Replace("namespace ArisenEngine", "namespace Arisen");
            patchedContent = patchedContent.Replace("global::ArisenBinding.", "ArisenBinding.");
            patchedContent = patchedContent.Replace("ArisenBinding.RHI.", "RHI.");
            patchedContent = patchedContent.Replace("global::System.", "System.");

            // 7. Remove redundant pragmas and noise
            patchedContent = patchedContent.Replace("#pragma warning disable CS0109 // Member does not hide an inherited member; new keyword is not required", "");

            // 8. Inject manual RHIHandle struct if this is the RHIHandle.cs file
            if (fileName == "RHIHandle.cs")
            {
                var rhiHandleStruct = @"
            [StructLayout(LayoutKind.Sequential)]
            public struct RHIHandle
            {
                public uint Index;
                public uint Generation;
                public bool IsValid => Index != 0xFFFFFFFFu;
                public static RHIHandle Invalid => new RHIHandle { Index = 0xFFFFFFFFu, Generation = 0 };
            }";
                // Target the empty RHI namespace block
                patchedContent = Regex.Replace(patchedContent, @"namespace RHI\s+\{\s+\}", "namespace RHI\r\n        {" + rhiHandleStruct + "\r\n        }");
            }

            // 9. Cleanup empty namespace blocks (including Std)
            patchedContent = patchedContent.Replace("namespace Std\r\n{\r\n}\r\n", "");
            patchedContent = patchedContent.Replace("namespace Std\n{\n}\n", "");
            
            // 10. Cleanup multiple empty lines (collapse 3+ newlines into 1)
            patchedContent = Regex.Replace(patchedContent, @"(\r?\n){3,}", "\r\n\r\n");

            if (content != patchedContent)
            {
                File.WriteAllText(file, patchedContent.Trim());
                Console.WriteLine($"Post-processed {relativePath} (DLL: {dllName})");
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