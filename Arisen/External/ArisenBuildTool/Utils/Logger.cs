using System;
using System.IO;

namespace ArisenBuildTool.Utils;

public static class Logger
{
    private static StreamWriter? s_FileWriter;

    public static void Initialize(string logFilePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            s_FileWriter = new StreamWriter(logFilePath, false) { AutoFlush = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to initialize file logger at {logFilePath}: {ex.Message}");
        }
    }

    public static void Info(string message)
    {
        Log("INFO", message, ConsoleColor.Gray);
    }

    public static void Warning(string message)
    {
        Log("WARN", message, ConsoleColor.Yellow);
    }

    public static void Error(string message)
    {
        Log("ERROR", message, ConsoleColor.Red);
    }

    private static void Log(string level, string message, ConsoleColor color)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        
        var defaultColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(formatted);
        Console.ForegroundColor = defaultColor;

        s_FileWriter?.WriteLine(formatted);
    }
}
