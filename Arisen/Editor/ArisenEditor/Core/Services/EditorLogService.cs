using System;
using ArisenEditorFramework.Services;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEditor.Core.Services;

public class EditorLogService : LogService
{
    public static event Action<Logger.LogMessage>? MessageAdded;

    public EditorLogService(string logFileName) : base(logFileName)
    {
    }

    public new void Info(string message)
    {
        base.Info(message);
        Notify(Logger.LogLevel.Info, message);
    }

    public void Log(string message)
    {
        base.Info(message);
        Notify(Logger.LogLevel.Log, message);
    }

    public new void Warning(string message)
    {
        base.Warning(message);
        Notify(Logger.LogLevel.Warning, message);
    }

    public new void Error(string message, Exception? ex = null)
    {
        base.Error(message, ex);
        Notify(Logger.LogLevel.Error, $"{message}{(ex != null ? $"\n{ex}" : "")}");
    }

    public new void Critical(string message, Exception? ex = null)
    {
        base.Critical(message, ex);
        Notify(Logger.LogLevel.Fatal, $"{message}{(ex != null ? $"\n{ex}" : "")}");
    }

    private void Notify(Logger.LogLevel level, string message)
    {
        var logMessage = new Logger.LogMessage(
            level, 
            message, 
            System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(), 
            System.Threading.Thread.CurrentThread.Name ?? "MainThread", 
            DateTime.Now, 
            Environment.StackTrace);

        MessageAdded?.Invoke(logMessage);
    }
}
