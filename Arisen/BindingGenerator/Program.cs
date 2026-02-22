using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // Verify that CppSharp actually generated output
        var csFiles = Directory.Exists(finalOutputDir)
            ? Directory.GetFiles(finalOutputDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                         && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToArray()
            : Array.Empty<string>();

        if (csFiles.Length == 0)
        {
            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("ERROR: CppSharp produced no .cs files!");
            Console.Error.WriteLine("Possible causes:");
            Console.Error.WriteLine("  - MSVC environment not initialized (VCToolsInstallDir, WindowsSdkDir missing)");
            Console.Error.WriteLine("  - Header include paths are incorrect");
            Console.Error.WriteLine($"  - Output dir: {finalOutputDir}");
            Console.Error.WriteLine($"  - Source dir:  {GlobalConfig.s_SourceCode}");
            Console.Error.WriteLine("========================================");
            Environment.Exit(1);
        }

        Console.WriteLine($"CppSharp generated {csFiles.Length} .cs file(s).");
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
    <PackageReference Include=""CppSharp"" Version=""1.1.5.3168"" />
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
            patchedContent = patchedContent.Replace("public __IntPtr __Instance { get; protected set; }", "internal __IntPtr __Instance { get; private protected set; }");
            patchedContent = patchedContent.Replace("internal __IntPtr __Instance { get; protected set; }", "internal __IntPtr __Instance { get; private protected set; }");
            patchedContent = patchedContent.Replace("public __IntPtr __Instance", "internal __IntPtr __Instance");
            // Keep public void Dispose() to avoid breaking IDisposable
            
            // Fix special case for RHILoader static Dispose vs instance Dispose conflict
            if (fileName == "RHILoader.cs")
            {
                patchedContent = patchedContent.Replace("public static void Dispose()", "public static void Unload()");
            }

            // 5. Remove redundant "Tag" classes (they are just markers and add 1000s of lines of noise)
            patchedContent = Regex.Replace(patchedContent, 
                @"    public unsafe partial class \w+Tag : IDisposable\s+\{.*?internal protected virtual void Dispose\(bool disposing, bool callNativeDtor \)\s+\{.*?\}\s+\}", 
                "", RegexOptions.Singleline);

            // 6. Cleanup the deep namespace prefixes
            // Unified replacement for ArisenEngine -> Arisen
            patchedContent = patchedContent.Replace("global::ArisenBinding.ArisenEngine.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("ArisenBinding.ArisenEngine.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("namespace ArisenEngine", "namespace Native"); // Note: usually nested in ArisenBinding
            patchedContent = patchedContent.Replace("namespace ArisenBinding", "namespace Arisen");

            // Explicitly map nested namespaces that should be under Arisen.Native
            patchedContent = patchedContent.Replace("ArisenBinding.RHI.", "Arisen.Native.RHI.");
            patchedContent = patchedContent.Replace("ArisenBinding.HAL.", "Arisen.Native.HAL.");
            patchedContent = patchedContent.Replace("ArisenBinding.Logger.", "Arisen.Native.Diagnostics."); // Map Logger to Diagnostics
            patchedContent = patchedContent.Replace("ArisenBinding.Assertion.", "Arisen.Native.Assertion.");
            
            // Map the old ArisenBinding prefix to Arisen.Native
            patchedContent = patchedContent.Replace("ArisenBinding.Arisen.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("ArisenBinding.", "Arisen.Native.");
            
            // Delegates and some global types stay under ArisenBinding directly
            // So we just remove the global:: prefix for them
            patchedContent = patchedContent.Replace("global::ArisenBinding.", "ArisenBinding.");
            patchedContent = patchedContent.Replace("global::System.", "System.");
            
            // Fix any accidental double Arisen introduced by overlapping replacements
            patchedContent = patchedContent.Replace("ArisenBinding.Arisen.Arisen.", "ArisenBinding.Arisen.");
            
            // Fix occasional cases where ArisenEngine escaped replacement
            patchedContent = patchedContent.Replace("ArisenEngine.RHI", "Arisen.RHI");
            patchedContent = patchedContent.Replace("ArisenEngine.HAL", "Arisen.HAL");
            
            // Map ArisenBinding.Arisen.String to string
            patchedContent = patchedContent.Replace("ArisenBinding.Arisen.String", "string");
            
            // Fix invalid 'new string.__Internal()' created by the above replacement
            // In these cases, it's likely a return-by-value string being marshaled.
            // But since we mapped ArisenEngine::String to C# string, CppSharp gets confused.
            // For now, nuke the 'new string.__Internal()' line and replace it with IntPtr.Zero
            patchedContent = patchedContent.Replace("var ___ret = new string.__Internal();", "var ___ret = System.IntPtr.Zero;");
            patchedContent = patchedContent.Replace("*(string*) @return", "*(System.IntPtr*) @return");
            patchedContent = patchedContent.Replace("new IntPtr(&___ret)", "___ret");
            
            // Emergency fix for broken UTF8Marshaller calls in generated code
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.InternalMarshalToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.InternalMarshalToManaged", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.NativeToUTF8", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.UTF8ToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("UTF8Marshaller.InternalMarshalToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("UTF8Marshaller.InternalMarshalToManaged", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("UTF8Marshaller.", "Arisen.Native.UTF8Marshaller.");

            // Fix GetDeviceLimits pointer conversion
            patchedContent = patchedContent.Replace(".GetDeviceLimits(__Instance, ___ret)", ".GetDeviceLimits(__Instance, new __IntPtr(&___ret))");

            // 7. Remove weird Std namespace blocks at the end of files (noise from ignored STL types)
            // Replace any internal use of Std types with IntPtr to avoid compilation errors
            patchedContent = Regex.Replace(patchedContent, @"global::Std\.\w+\.__Internal\w*", "global::System.IntPtr");
            patchedContent = Regex.Replace(patchedContent, @"Std\.\w+\.__Internal\w*", "global::System.IntPtr");
            patchedContent = Regex.Replace(patchedContent, @"global::Std\.\w+", "global::System.IntPtr");
            patchedContent = Regex.Replace(patchedContent, @"Std\.\w+", "global::System.IntPtr");
            
            // Fix EProgramStage namespace mapping if it's being used from RHI
            patchedContent = patchedContent.Replace("ArisenBinding.EProgramStage", "ArisenBinding.Arisen.RHI.EProgramStage");
            
            // Nuke all namespace Std blocks (including nested ones)
            patchedContent = Regex.Replace(patchedContent, @"namespace Std\s+\{.*?\r?\n\}", "", RegexOptions.Singleline);
            // Catch any multi-level ones
            patchedContent = Regex.Replace(patchedContent, @"namespace Std\s+\{.*?\n\s+\}\r?\n?\}", "", RegexOptions.Singleline);

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

        // Generate UTF8Marshaller.cs
        GenerateMarshaller(rootDir);
    }

    static void GenerateMarshaller(string outputDir)
    {
        var path = Path.Combine(outputDir, "UTF8Marshaller.cs");
        var content = @"using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Arisen.Native
{
    public static class UTF8Marshaller
    {
        public static string NativeToUTF8(IntPtr nativePtr)
        {
            if (nativePtr == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUTF8(nativePtr) ?? string.Empty;
        }

        public static unsafe string NativeToUTF8(IntPtr* nativePtr)
        {
            if (nativePtr == null || *nativePtr == IntPtr.Zero) return string.Empty;
            return Marshal.PtrToStringUTF8(*nativePtr) ?? string.Empty;
        }

        public static IntPtr UTF8ToNative(string str)
        {
            if (str == null) return IntPtr.Zero;
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }
    }
}
";
        File.WriteAllText(path, content);
        Console.WriteLine("Generated UTF8Marshaller.cs");
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