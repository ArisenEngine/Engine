using System;
using ArisenKernel.Contracts;

namespace ArisenKernel.Diagnostics;

/// <summary>
/// A static facade for the Kernel and Packages to log easily. 
/// It attempts to use the registered ILogger from the ServiceRegistry. 
/// If none is registered yet (e.g., during early boot), it falls back to the system console.
/// P2: Caches the logger reference to avoid per-call dictionary lookup.
/// B13: Uses IsValueCreated to avoid premature kernel instantiation.
/// </summary>
public static class KernelLog
{
    private static ILogger? s_CachedLogger;
    private static bool s_LoggerLookupAttempted;

    private static ILogger? GetLogger()
    {
        // P2: Return cached logger if already resolved
        if (s_CachedLogger != null) return s_CachedLogger;

        // B13: Don't access EngineKernel.Instance if it hasn't been created yet —
        // accessing it triggers Lazy<> initialization
        if (!s_LoggerLookupAttempted && Lifecycle.EngineKernel.IsCreated)
        {
            var kernel = Lifecycle.EngineKernel.Instance;
            if (kernel.Services.TryGetService<ILogger>(out var logger))
            {
                s_CachedLogger = logger;
                return s_CachedLogger;
            }

            // Only stop re-attempting if we've passed the initialization phases.
            // If we are still in PreInit/Init/PostInit, we keep trying as services 
            // might still be registering (e.g. from late-loading packages).
            if (kernel.CurrentPhase >= Lifecycle.EnginePhase.Running)
            {
                s_LoggerLookupAttempted = true;
            }
        }
        return null;
    }

    /// <summary>
    /// Invalidates the cached logger, forcing a new lookup on next call.
    /// Should be called when services are re-registered (e.g., after kernel Reset).
    /// </summary>
    public static void InvalidateCache()
    {
        s_CachedLogger = null;
        s_LoggerLookupAttempted = false;
    }

    public static void Info(string message)
    {
        var logger = GetLogger();
        if (logger != null) logger.Log(message);
        else Console.WriteLine($"[INFO] {message}");
    }

    public static void InfoFormat(string format, params object[] args)
    {
        var logger = GetLogger();
        if (logger != null) logger.LogFormat(format, args);
        else Console.WriteLine($"[INFO] {string.Format(format, args)}");
    }

    public static void Warning(string message)
    {
        var logger = GetLogger();
        if (logger != null) logger.Warning(message);
        else Console.WriteLine($"[WARN] {message}");
    }

    public static void WarningFormat(string format, params object[] args)
    {
        var logger = GetLogger();
        if (logger != null) logger.WarningFormat(format, args);
        else Console.WriteLine($"[WARN] {string.Format(format, args)}");
    }

    public static void Error(string message)
    {
        var logger = GetLogger();
        if (logger != null) logger.Error(message);
        else Console.WriteLine($"[ERROR] {message}");
    }

    public static void ErrorFormat(string format, params object[] args)
    {
        var logger = GetLogger();
        if (logger != null) logger.ErrorFormat(format, args);
        else Console.WriteLine($"[ERROR] {string.Format(format, args)}");
    }

    public static void Fatal(string message)
    {
        var logger = GetLogger();
        if (logger != null) logger.Fatal(message);
        else Console.WriteLine($"[FATAL] {message}");
    }

    public static void FatalFormat(string format, params object[] args)
    {
        var logger = GetLogger();
        if (logger != null) logger.FatalFormat(format, args);
        else Console.WriteLine($"[FATAL] {string.Format(format, args)}");
    }

    public static void Assert(bool condition, string message = "")
    {
        var logger = GetLogger();
        if (logger != null) 
        {
            logger.Assert(condition, message);
        }
        else if (!condition)
        {
            Console.WriteLine($"[ASSERT FAILED] {message}");
            System.Diagnostics.Debug.Assert(condition, message);
        }
    }

    public static void AssertFormat(bool condition, string format, params object[] args)
    {
        var logger = GetLogger();
        if (logger != null) 
        {
            logger.AssertFormat(condition, format, args);
        }
        else if (!condition)
        {
            Console.WriteLine($"[ASSERT FAILED] {string.Format(format, args)}");
            System.Diagnostics.Debug.Assert(condition, string.Format(format, args));
        }
    }
}

