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


            // 6. Cleanup the deep namespace prefixes
            // Step 1: Unified root namespace
            patchedContent = patchedContent.Replace("namespace ArisenBinding", "namespace Arisen.Native");
            patchedContent = patchedContent.Replace("namespace ArisenEngine", "namespace Arisen.Native");
            patchedContent = patchedContent.Replace("namespace Native", "namespace Arisen.Native");

            // Step 2: Balanced-brace flattener for nested namespaces
            // We turn: namespace Arisen.Native { namespace Sub { ... } } -> namespace Arisen.Native.Sub { ... }
            bool changed = true;
            while (changed)
            {
                changed = false;
                // .NET Balanced Groups Regex to find a nested namespace block and its matching braces
                var pattern = @"(namespace\s+Arisen\.Native[\w\.]*)\s*\{\s*namespace\s+([\w\.]+)\s*\{((?>[^{}]+|(?<open>{)|(?<-open>}))*)\}\s*\}";
                patchedContent = Regex.Replace(patchedContent, pattern, (m) => {
                    var ns1 = m.Groups[1].Value.Trim();
                    var ns2 = m.Groups[2].Value.Trim();
                    var inner = m.Groups[3].Value;
                    changed = true;
                    if (ns2 == "Arisen.Native" || ns2 == "Native" || ns2 == "ArisenEngine")
                        return $"{ns1} {{{inner}}}";
                    return $"{ns1}.{ns2} {{{inner}}}";
                }, RegexOptions.Singleline);
            }

            // Step 3: Global type reference cleanup
            patchedContent = patchedContent.Replace("global::ArisenBinding.ArisenEngine.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("ArisenBinding.ArisenEngine.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("global::ArisenBinding.Arisen.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("ArisenBinding.Arisen.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("global::ArisenBinding.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("ArisenBinding.", "Arisen.Native.");

            // Handle underscore-based names in delegates (CppSharp style)
            patchedContent = patchedContent.Replace("ArisenBinding_ArisenEngine_", "Arisen_Native_");
            patchedContent = patchedContent.Replace("ArisenBinding_Arisen_", "Arisen_Native_");
            patchedContent = patchedContent.Replace("ArisenBinding_", "Arisen_Native_");

            // Step 4: Fix __Internal references in Internal subclasses — scoped to each class body only
            // IMPORTANT: We must NOT do global String.Replace here, as that would corrupt
            // unrelated classes in the same file (e.g. LogSourceLocation, RHIInstanceInfo).
            // Instead, use balanced-brace regex to find each *Internal class body and replace only within it.
            patchedContent = Regex.Replace(patchedContent,
                @"(public\s+unsafe\s+partial\s+class\s+(\w+)Internal\s*:\s*([\w\.]+)\.(\w+)[^{]*\{)((?>[^{}]+|(?<open>\{)|(?<-open>\}))*)(\})",
                m => {
                    string baseClassName = m.Groups[4].Value;
                    string body = m.Groups[5].Value;
                    body = body.Replace("(__Internal*)", $"({baseClassName}.__Internal*)");
                    body = body.Replace("sizeof(__Internal)", $"sizeof({baseClassName}.__Internal)");
                    body = body.Replace("new __Internal()", $"new {baseClassName}.__Internal()");
                    body = body.Replace("(__Internal native", $"({baseClassName}.__Internal native");
                    return m.Groups[1].Value + body + m.Groups[6].Value;
                }, RegexOptions.Singleline);

            // Step 5: Marshalling and specific type fixes
            patchedContent = patchedContent.Replace("ArisenBinding.Arisen.String", "string");
            patchedContent = patchedContent.Replace("new Arisen.Native.String.__Internal()", "System.IntPtr.Zero");
            patchedContent = patchedContent.Replace("Arisen.Native.String", "string");
            patchedContent = patchedContent.Replace("var ___ret = new string.__Internal();", "var ___ret = System.IntPtr.Zero;");
            patchedContent = patchedContent.Replace("*(string*) @return", "*(System.IntPtr*) @return");
            patchedContent = patchedContent.Replace("new IntPtr(&___ret)", "___ret");
            
            // Fix string fields in __Internal structs — C++ std::string/ArisenEngine::String
            // fields are native pointers, not managed C# strings
            patchedContent = Regex.Replace(patchedContent, 
                @"(internal\s+)string(\s+\w+;)", "$1__IntPtr$2");
            
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.InternalMarshalToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.InternalMarshalToManaged", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.NativeToUTF8", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("CppSharp.Runtime.UTF8Marshaller.UTF8ToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("UTF8Marshaller.InternalMarshalToNative", "Arisen.Native.UTF8Marshaller.UTF8ToNative");
            patchedContent = patchedContent.Replace("UTF8Marshaller.InternalMarshalToManaged", "Arisen.Native.UTF8Marshaller.NativeToUTF8");
            patchedContent = patchedContent.Replace("UTF8Marshaller.", "Arisen.Native.UTF8Marshaller.");

            patchedContent = patchedContent.Replace(".GetDeviceLimits(__Instance, ___ret)", ".GetDeviceLimits(__Instance, new __IntPtr(&___ret))");

            // Final redundant prefix cleanup (must be last!)
            patchedContent = patchedContent.Replace("Arisen.Native.Arisen.Native.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("Arisen.Native.ArisenEngine.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("Arisen.Native.Native.", "Arisen.Native.");
            patchedContent = patchedContent.Replace("Arisen.Native..", "Arisen.Native.");
            patchedContent = Regex.Replace(patchedContent, @"(Arisen\.Native\.){2,}", "Arisen.Native.");

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
                // Target the Arisen.Native.RHI namespace block (safe for both flattened and original)
                patchedContent = patchedContent.Replace("namespace Arisen.Native.RHI {", "namespace Arisen.Native.RHI\r\n    {" + rhiHandleStruct);
                patchedContent = patchedContent.Replace("namespace Arisen.Native.RHI\n{", "namespace Arisen.Native.RHI\n    {" + rhiHandleStruct);
                patchedContent = patchedContent.Replace("namespace RHI\r\n        {", "namespace RHI\r\n        {" + rhiHandleStruct);
            }

            // 8b. Inject missing uint32_t properties into RHIInstanceInfo
            // CppSharp ignored these because it couldn't resolve uint32_t properties,
            // but the __Internal struct already has the fields.
            if (fileName == "RHIInstance.cs")
            {
                var uint32Props = new[] {
                    ("variant", "Variant"),
                    ("major", "Major"),
                    ("minor", "Minor"),
                    ("patch", "Patch"),
                    ("appMajor", "AppMajor"),
                    ("appMinor", "AppMinor"),
                    ("appPatch", "AppPatch"),
                    ("engineMajor", "EngineMajor"),
                    ("engineMinor", "EngineMinor"),
                    ("enginePatch", "EnginePatch"),
                    ("maxFramesInFlight", "MaxFramesInFlight")
                };
                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                foreach (var (field, prop) in uint32Props)
                {
                    sb.AppendLine($"                public uint {prop}");
                    sb.AppendLine( "                {");
                    sb.AppendLine($"                    get {{ return ((__Internal*)__Instance)->{field}; }}");
                    sb.AppendLine($"                    set {{ ((__Internal*)__Instance)->{field} = value; }}");
                    sb.AppendLine( "                }");
                    sb.AppendLine();
                }
                // Inject before the closing brace of ValidationLayer property's class
                // The pattern: the closing "}" of the ValidationLayer property set block, followed by class close
                patchedContent = patchedContent.Replace(
                    "                    ((__Internal*)__Instance)->validationLayer = (byte) (value ? 1 : 0);\r\n                    }\r\n                }\r\n            }",
                    "                    ((__Internal*)__Instance)->validationLayer = (byte) (value ? 1 : 0);\r\n                    }\r\n                }\r\n" + sb.ToString() + "            }");
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

        // Generate RenderWindowAPI.cs — manual P/Invoke wrapper because CppSharp
        // cannot process extern "C" free functions with Windows-specific types (HWND, WindowProc)
        GenerateRenderWindowAPI(rootDir);
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

    static void GenerateRenderWindowAPI(string outputDir)
    {
        var path = Path.Combine(outputDir, "RenderWindowAPI.cs");
        var content = @"// ----------------------------------------------------------------------------
// Manual P/Invoke wrapper for RenderWindowAPI
// CppSharp cannot generate this because the C++ header uses extern ""C"" free
// functions with Windows-specific types (HWND, WindowProc) that fail AST resolution.
// ----------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Security;
using __CallingConvention = global::System.Runtime.InteropServices.CallingConvention;

namespace Arisen.Native.HAL
{
    public static class RenderWindowAPI
    {
        private const string DllName = ""Core.HAL.dll"";

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint CreateFullScreenRenderSurface(IntPtr host, IntPtr callback);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint CreateRenderWindow(IntPtr host, IntPtr callback, int width, int height);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint CreateRenderWindowWithResizeCallback(
            IntPtr host, IntPtr callback, IntPtr resizeCallback, IntPtr resizingCallback, int width, int height);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern void RemoveRenderSurface(uint id);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern void ResizeRenderSurface(uint id, uint width, uint height);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern IntPtr GetWindowHandle(uint id);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint GetWindowWidth(uint id);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint GetWindowHeight(uint id);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern uint GetWindowId(IntPtr handle);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern void SetWindowResizeCallback(uint windowId, IntPtr callback);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern void SetWindowResizingCallback(uint windowId, IntPtr callback);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern IntPtr GetWindowUserData(uint windowId);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = __CallingConvention.Cdecl)]
        public static extern void SetWindowUserData(uint windowId, IntPtr data);
    }
}
";
        File.WriteAllText(path, content);
        Console.WriteLine("Generated RenderWindowAPI.cs");
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