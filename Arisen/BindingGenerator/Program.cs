// Program.cs

using BindingGenerator.Debugger;
using CppSharp;

namespace BindingGenerator;

internal static class Program
{
    static void Main(string[] args)
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

        Console.WriteLine($"Start Generate Binding, source code root : {GlobalConfig.s_SourceCode} , output root : {GlobalConfig.s_Output}, library : {GlobalConfig.s_LibraryPath}");

        DeleteDirectory(
            Path.GetFullPath(Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName)),
            new List<string>() { "obj" }, new List<string>() { ".csproj" });

        ConsoleDriver.Run(new DebuggerLibrary());
        ConsoleDriver.Run(new PlatformLibrary());
    }

    static void DeleteDirectory(string directoryPath, List<string> skipFolders, List<string> excludedExtensions)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);
        if (skipFolders.Contains(directoryInfo.Name))
            return;

        bool hasSkipFile = false;
        var files = Directory.GetFiles(directoryPath);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (excludedExtensions.Count > 0 && excludedExtensions.Contains(fileInfo.Extension))
            {
                hasSkipFile = true;
                continue;
            }

            File.Delete(file);
        }

        foreach (var directory in Directory.GetDirectories(directoryPath))
        {
            DeleteDirectory(directory, skipFolders, excludedExtensions);
        }

        if (!hasSkipFile)
            Directory.Delete(directoryPath);
    }
}