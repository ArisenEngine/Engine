using System;
using ArisenEditorFramework.Services;

namespace ArisenEditor.Core.Services;

public static class EditorLog
{
    private static ILogService? _instance;

    public static void Initialize(ILogService instance)
    {
        _instance = instance;
    }

    public static void Info(string message) => _instance?.Info(message);
    public static void Log(string message) => _instance?.Info(message);
    public static void Warning(string message) => _instance?.Warning(message);
    public static void Error(string message, Exception? ex = null) => _instance?.Error(message, ex);
    public static void Critical(string message, Exception? ex = null) => _instance?.Critical(message, ex);
    
    public static string GetLogPath() => _instance?.GetLogPath() ?? string.Empty;
}
