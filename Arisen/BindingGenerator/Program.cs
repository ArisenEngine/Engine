using System;
using System.Collections.Generic;
using System.IO;
using CppSharp;
using BindingGenerator.Modules;

namespace BindingGenerator;

internal static class Program
{
    static void Main(string[] args)
    {
        ParseArguments(args);

        Console.WriteLine($"Source: {GlobalConfig.s_SourceCode}");
        Console.WriteLine($"Output: {GlobalConfig.s_Output}");
        Console.WriteLine($"Library: {GlobalConfig.s_LibraryPath}");

        if (string.IsNullOrEmpty(GlobalConfig.s_SourceCode) || string.IsNullOrEmpty(GlobalConfig.s_Output))
        {
            Console.WriteLine("Missing required arguments: --source_code and --output");
            return;
        }

        // Clean output directory
        var finalOutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName);
        if (Directory.Exists(finalOutputDir))
        {
            DeleteDirectory(finalOutputDir, new List<string>(), new List<string>());
        }

        // Execute Modules
        var modules = new List<ILibrary>
        {
            new CoreModule(),
            new PlatformModule(),
            new DebuggerModule()
        };

        foreach (var module in modules)
        {
            Console.WriteLine($"Generating binding for {module.GetType().Name}...");
            ConsoleDriver.Run(module);
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

    static void DeleteDirectory(string directoryPath, List<string> skipFolders, List<string> excludedExtensions)
    {
        if (!Directory.Exists(directoryPath)) return;
        foreach (var file in Directory.GetFiles(directoryPath))
        {
            if (!excludedExtensions.Contains(Path.GetExtension(file)))
                File.Delete(file);
        }
        foreach (var dir in Directory.GetDirectories(directoryPath))
        {
            if (!skipFolders.Contains(Path.GetFileName(dir)))
                DeleteDirectory(dir, skipFolders, excludedExtensions);
        }
    }
}