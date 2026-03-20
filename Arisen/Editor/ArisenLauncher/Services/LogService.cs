using System;
using System.IO;

namespace ArisenLauncher.Services;

public interface ILogService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
    void Critical(string message, Exception? ex = null);
}

public class LogService : ILogService
{
    private readonly string _logFilePath;

    public LogService(string filename)
    {
        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", filename);
        // Clear old log
        if (File.Exists(_logFilePath)) File.Delete(_logFilePath);
    }

    private void Log(string level, string message, Exception? ex)
    {
        var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        if (ex != null)
        {
            logLine += $"\nException: {ex}";
        }
        
        System.Diagnostics.Debug.WriteLine(logLine);
        Console.WriteLine(logLine);
        
        try
        {
            File.AppendAllText(_logFilePath, logLine + "\n");
        }
        catch { }
    }

    public void Info(string message) => Log("INFO", message, null);
    public void Warning(string message) => Log("WARN", message, null);
    public void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);
    public void Critical(string message, Exception? ex = null) => Log("CRITICAL", message, ex);
}
