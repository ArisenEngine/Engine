namespace ArisenKernel.Contracts;

public enum LogLevel
{
    Log,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Contract for logging within the engine. 
/// Packages and the Kernel use this to output diagnostic information.
/// </summary>
public interface ILogger
{
    void Log(string message);
    void LogFormat(string format, params object[] args);
    
    void Warning(string message);
    void WarningFormat(string format, params object[] args);
    
    void Error(string message);
    void ErrorFormat(string format, params object[] args);
    
    void Fatal(string message);
    void FatalFormat(string format, params object[] args);

    void Assert(bool condition, string message = "");
    void AssertFormat(bool condition, string format, params object[] args);
}
