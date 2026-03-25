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

    public LogService(string filename)
    {
        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        // B17: Ensure the logs directory exists before attempting to write
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

        string logFilePath = Path.Combine(logDir, filename);
        // Clear old log and open a persistent, buffered writer
        // P7: StreamWriter is far more efficient than per-line File.AppendAllText
        _writer = new StreamWriter(logFilePath, append: false) { AutoFlush = true };
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
