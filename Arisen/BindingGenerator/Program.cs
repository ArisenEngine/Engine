using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BindingGenerator.Generator;

namespace BindingGenerator;

internal static class Program
{
    // ========================================================================
    // Arisen Engine Binding Generator
    // ========================================================================
    // Scans C++ headers for ARISEN_BIND_* annotation macros and generates
    // clean C# P/Invoke code. No CppSharp, no libclang, no post-processing.
    // ========================================================================

    static string s_SourceCode = "";
    static string s_Output = "";
    static readonly string s_GenerationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    static void Main(string[] args)
    {
        ParseArguments(args);

        if (string.IsNullOrEmpty(s_SourceCode) || string.IsNullOrEmpty(s_Output))
        {
            Console.WriteLine("Usage: BindingGenerator --source_code <path> --output <path> [--library <path>]");
            Console.WriteLine("  --source_code  Root directory containing C++ headers");
            Console.WriteLine("  --output       Root directory for generated C# output");
            return;
        }

        var packageRoots = new Dictionary<string, string>();
        var cleanedPackageRoots = new HashSet<string>();

        // Scan all headers
        // Scan both .h and .cpp files (bridges are in .cpp)
        var headers = Directory.GetFiles(s_SourceCode, "*.h", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(s_SourceCode, "*.cpp", SearchOption.AllDirectories))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "3rdparty" + Path.DirectorySeparatorChar))
            .ToList();

        Console.WriteLine($"Scanning {headers.Count} source files...");

        int generatedFiles = 0;
        var allGeneratedFiles = new List<string>();
        var packagesModified = new HashSet<string>();

        foreach (var header in headers)
        {
            // Skip the macro definitions file itself
            if (Path.GetFileName(header) == "BindingMacros.h")
                continue;

            var content = File.ReadAllText(header);

            // Skip files without binding annotations
            if (!content.Contains("ARISEN_BIND_"))
                continue;

            var relativePath = Path.GetRelativePath(s_SourceCode, header);
            Console.WriteLine($"  Processing: {relativePath}");

            var results = CSharpGenerator.ProcessHeader(content, header, s_GenerationTime);
            if (results.Count == 0) continue;

            var packageRoot = FindPackageRoot(header);
            if (packageRoot == null)
            {
                Console.WriteLine($"    [WARN] Could not find package.json for {header}. Skipping bindings.");
                continue;
            }

            if (!cleanedPackageRoots.Contains(packageRoot))
            {
                var cleanDir = Path.Combine(packageRoot, "Managed", "Generated");
                if (Directory.Exists(cleanDir)) CleanOutputDirectory(cleanDir);
                cleanedPackageRoots.Add(packageRoot);
            }

            foreach (var (packageId, fileName, csContent, subDir) in results)
            {
                packageRoots[packageId] = packageRoot;
                packagesModified.Add(packageId);

                var generatedRoot = Path.Combine(packageRoot, "Managed", "Generated");
                var targetDir = string.IsNullOrEmpty(subDir) ? generatedRoot : Path.Combine(generatedRoot, subDir);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                var outPath = Path.Combine(targetDir, fileName);
                File.WriteAllText(outPath, csContent);
                var displayPath = string.IsNullOrEmpty(subDir) ? fileName : $"{subDir}/{fileName}";
                allGeneratedFiles.Add(displayPath);
                generatedFiles++;
                Console.WriteLine($"    Generated [{packageId}]: {displayPath}");
            }
        }

        // Generate UTF8Marshaller and Package Metadata per package
        foreach (var pkg in packagesModified)
        {
             if (packageRoots.TryGetValue(pkg, out var pRoot))
             {
                 var pOut = Path.Combine(pRoot, "Managed", "Generated");
                 MarshallerGenerator.GenerateMarshaller(pOut, s_GenerationTime);
             }
        }

        Console.WriteLine($"\nGeneration complete: {generatedFiles} file(s) generated across {packagesModified.Count} package(s).");
    }

    static void ParseArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source_code" && i + 1 < args.Length)
                s_SourceCode = args[i + 1];
            else if (args[i] == "--output" && i + 1 < args.Length)
                s_Output = args[i + 1];
            // --library is accepted but not used in the new generator
        }
    }

    static void CleanOutputDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;

        // Clean .cs files recursively (including subdirectories like RHI/)
        foreach (var file in Directory.GetFiles(dirPath, "*.cs", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        // Remove empty subdirectories (skip bin/obj)
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            var name = Path.GetFileName(dir);
            if (name == "bin" || name == "obj") continue;
            if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                Directory.Delete(dir, true);
        }
    }

    static string FindPackageRoot(string startPath)
    {
        var dir = Path.GetDirectoryName(startPath);
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "package.json"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}