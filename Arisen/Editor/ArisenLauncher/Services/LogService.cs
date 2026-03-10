using System;
using System.IO;

namespace ArisenLauncher.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Critical
}

public class LogService
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LogService()
    {
        string logDir = AppContext.BaseDirectory;
        _logFilePath = Path.Combine(logDir, "launcher.log");
        
        // Start fresh log session
        Info("=== Arisen Launcher Session Started ===");
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, $"{message}{(ex != null ? $"\nException: {ex}" : "")}");
    public void Critical(string message, Exception? ex = null) => Log(LogLevel.Critical, $"{message}{(ex != null ? $"\nException: {ex}" : "")}");

    private void Log(LogLevel level, string message)
    {
        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {message}";
        
        // Output to console for debugging
        Console.WriteLine(logEntry);

        lock (_lock)
        {
            try
            {
                File.AppendAllLines(_logFilePath, new[] { logEntry });
            }
            catch
            {
                // Fallback if file writing fails
            }
        }
    }

    public string GetLogPath() => _logFilePath;
}
