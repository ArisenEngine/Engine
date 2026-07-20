using System.Threading;

namespace ArisenKernel.Lifecycle;

public interface IRuntimeVisualSummaryService
{
    bool IsEnabled { get; }
    uint CaptureFrameIndex { get; }
    string ProfileName { get; }
    string OutputPath { get; }
    bool IsComplete { get; }
    bool Succeeded { get; }
    string? FailureMessage { get; }

    bool TryBeginCapture(uint frameIndex);
    void ReportSuccess();
    void ReportFailure(string message);
}

internal sealed class RuntimeVisualSummaryService : IRuntimeVisualSummaryService
{
    private const int Pending = 0;
    private const int Capturing = 1;
    private const int Complete = 2;
    private const int Failed = 3;

    private int m_State;
    private string? m_FailureMessage;

    public RuntimeVisualSummaryService(
        string workspacePath,
        string profileName,
        uint captureFrameIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        CaptureFrameIndex = captureFrameIndex;
        ProfileName = profileName;
        OutputPath = GetDefaultOutputPath(workspacePath, profileName);
    }

    public bool IsEnabled => true;
    public uint CaptureFrameIndex { get; }
    public string ProfileName { get; }
    public string OutputPath { get; }
    public bool IsComplete => Volatile.Read(ref m_State) is Complete or Failed;
    public bool Succeeded => Volatile.Read(ref m_State) == Complete;
    public string? FailureMessage => Volatile.Read(ref m_FailureMessage);

    public bool TryBeginCapture(uint frameIndex)
    {
        return frameIndex == CaptureFrameIndex &&
               Interlocked.CompareExchange(ref m_State, Capturing, Pending) == Pending;
    }

    public void ReportSuccess()
    {
        if (Interlocked.CompareExchange(ref m_State, Complete, Capturing) != Capturing)
        {
            throw new InvalidOperationException(
                "Runtime visual summary can only complete after capture begins.");
        }
    }

    public void ReportFailure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        while (true)
        {
            var state = Volatile.Read(ref m_State);
            if (state is Complete or Failed)
            {
                return;
            }

            Volatile.Write(ref m_FailureMessage, message);
            if (Interlocked.CompareExchange(ref m_State, Failed, state) == state)
            {
                return;
            }
        }
    }

    private static string GetDefaultOutputPath(string workspacePath, string profileName)
    {
        var safeProfileName = profileName;
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            safeProfileName = safeProfileName.Replace(invalidCharacter, '_');
        }

        return Path.GetFullPath(Path.Combine(
            workspacePath,
            ".arisen",
            "Logs",
            $"visual-summary-{safeProfileName}-latest.json"));
    }
}
