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

public class LogService : ILogService, IDisposable
{
    private readonly StreamWriter _writer;

    public LogService()
    {
        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

        // P8: Create a unique log file for every launch with a timestamp
        // Windows filenames cannot contain ':', so we use '-' for the time part
        string timestamp = DateTime.Now.ToString("yyyy-M-d HH-mm-ss.fff");
        string filename = $"log-{timestamp}.log";
        string logFilePath = Path.Combine(logDir, filename);
        
        // Use FileShare.ReadWrite to allow log viewers (like Notepad++) to access the file while the launcher is running
        var stream = new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
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
            _writer.WriteLine(logLine);
        }
        catch { }
    }

    public void Info(string message) => Log("INFO", message, null);
    public void Warning(string message) => Log("WARN", message, null);
    public void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);
    public void Critical(string message, Exception? ex = null) => Log("CRITICAL", message, ex);

    public void Dispose()
    {
        _writer.Dispose();
    }
}
