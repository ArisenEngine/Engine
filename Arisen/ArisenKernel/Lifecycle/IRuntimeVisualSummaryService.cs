using System.Threading;

namespace ArisenKernel.Lifecycle;

public enum RuntimeVisualSummaryCaptureState
{
    Scheduled,
    Capturing,
    Succeeded,
    Failed
}

public readonly record struct RuntimeVisualSummaryCapture(
    long Sequence,
    string Name,
    uint FrameIndex,
    string OutputPath);

public sealed record RuntimeVisualSummaryCaptureResult(
    RuntimeVisualSummaryCapture Capture,
    RuntimeVisualSummaryCaptureState State,
    string? FailureMessage);

public interface IRuntimeVisualSummaryService
{
    bool IsEnabled { get; }
    uint CaptureFrameIndex { get; }
    string ProfileName { get; }
    string OutputPath { get; }
    bool IsComplete { get; }
    bool Succeeded { get; }
    string? FailureMessage { get; }

    bool TryScheduleCapture(string name, uint frameIndex, out string outputPath);
    bool TryBeginCapture(uint frameIndex, out RuntimeVisualSummaryCapture capture);
    void ReportSuccess(RuntimeVisualSummaryCapture capture);
    void ReportFailure(RuntimeVisualSummaryCapture capture, string message);
    void ReportFailure(string message);
    bool TryGetCaptureResult(string name, out RuntimeVisualSummaryCaptureResult result);
    IReadOnlyList<RuntimeVisualSummaryCaptureResult> GetCaptureResults();
    void Seal();
}

internal sealed class RuntimeVisualSummaryService : IRuntimeVisualSummaryService
{
    private readonly object m_Gate = new();
    private readonly List<CaptureEntry> m_Captures = new();
    private long m_NextSequence;
    private bool m_Sealed;
    private string? m_FailureMessage;

    public RuntimeVisualSummaryService(
        string workspacePath,
        string profileName,
        uint captureFrameIndex,
        string? outputPath = null)
        : this(workspacePath, profileName, outputPath)
    {
        TryScheduleCapture("final", captureFrameIndex, out _);
        Seal();
    }

    public RuntimeVisualSummaryService(
        string workspacePath,
        string profileName,
        string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        ProfileName = profileName;
        OutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? GetDefaultOutputPath(workspacePath, profileName)
            : Path.GetFullPath(outputPath);
    }

    public bool IsEnabled => true;

    public uint CaptureFrameIndex
    {
        get
        {
            lock (m_Gate)
            {
                CaptureEntry? pending = m_Captures
                    .Where(entry => entry.State == RuntimeVisualSummaryCaptureState.Scheduled)
                    .OrderBy(entry => entry.Capture.FrameIndex)
                    .ThenBy(entry => entry.Capture.Sequence)
                    .FirstOrDefault();
                return pending?.Capture.FrameIndex ?? uint.MaxValue;
            }
        }
    }

    public string ProfileName { get; }
    public string OutputPath { get; }

    public bool IsComplete
    {
        get
        {
            lock (m_Gate) return IsCompleteLocked();
        }
    }

    public bool Succeeded
    {
        get
        {
            lock (m_Gate)
            {
                return IsCompleteLocked() && m_Captures.All(entry =>
                    entry.State == RuntimeVisualSummaryCaptureState.Succeeded);
            }
        }
    }

    public string? FailureMessage => Volatile.Read(ref m_FailureMessage);

    public bool TryScheduleCapture(string name, uint frameIndex, out string outputPath)
    {
        string canonicalName = ValidateCaptureName(name);
        lock (m_Gate)
        {
            if (m_Sealed || m_Captures.Any(entry =>
                    string.Equals(entry.Capture.Name, canonicalName, StringComparison.Ordinal)))
            {
                outputPath = string.Empty;
                return false;
            }

            outputPath = string.Equals(canonicalName, "final", StringComparison.Ordinal)
                ? OutputPath
                : GetNamedOutputPath(OutputPath, canonicalName);
            var capture = new RuntimeVisualSummaryCapture(
                ++m_NextSequence,
                canonicalName,
                frameIndex,
                outputPath);
            m_Captures.Add(new CaptureEntry(capture));
            return true;
        }
    }

    public bool TryBeginCapture(uint frameIndex, out RuntimeVisualSummaryCapture capture)
    {
        lock (m_Gate)
        {
            foreach (CaptureEntry missed in m_Captures.Where(entry =>
                         entry.State == RuntimeVisualSummaryCaptureState.Scheduled &&
                         entry.Capture.FrameIndex < frameIndex))
            {
                FailLocked(
                    missed,
                    $"Visual-summary capture '{missed.Capture.Name}' missed requested frame " +
                    $"{missed.Capture.FrameIndex}.");
            }

            CaptureEntry? entry = m_Captures
                .Where(candidate =>
                    candidate.State == RuntimeVisualSummaryCaptureState.Scheduled &&
                    candidate.Capture.FrameIndex == frameIndex)
                .OrderBy(candidate => candidate.Capture.Sequence)
                .FirstOrDefault();
            if (entry == null)
            {
                capture = default;
                return false;
            }

            entry.State = RuntimeVisualSummaryCaptureState.Capturing;
            capture = entry.Capture;
            return true;
        }
    }

    public void ReportSuccess(RuntimeVisualSummaryCapture capture)
    {
        lock (m_Gate)
        {
            CaptureEntry entry = ResolveCapturingLocked(capture);
            entry.State = RuntimeVisualSummaryCaptureState.Succeeded;
        }
    }

    public void ReportFailure(RuntimeVisualSummaryCapture capture, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (m_Gate)
        {
            CaptureEntry entry = ResolveLocked(capture);
            if (entry.State is RuntimeVisualSummaryCaptureState.Succeeded or
                RuntimeVisualSummaryCaptureState.Failed)
            {
                return;
            }

            FailLocked(entry, message);
        }
    }

    public void ReportFailure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (m_Gate)
        {
            foreach (CaptureEntry entry in m_Captures.Where(entry =>
                         entry.State is RuntimeVisualSummaryCaptureState.Scheduled or
                         RuntimeVisualSummaryCaptureState.Capturing))
            {
                FailLocked(entry, message);
            }

            m_Sealed = true;
            Volatile.Write(ref m_FailureMessage, message);
        }
    }

    public bool TryGetCaptureResult(string name, out RuntimeVisualSummaryCaptureResult result)
    {
        lock (m_Gate)
        {
            CaptureEntry? entry = m_Captures.FirstOrDefault(candidate =>
                string.Equals(candidate.Capture.Name, name, StringComparison.Ordinal));
            if (entry == null)
            {
                result = null!;
                return false;
            }

            result = entry.Result();
            return true;
        }
    }

    public IReadOnlyList<RuntimeVisualSummaryCaptureResult> GetCaptureResults()
    {
        lock (m_Gate)
        {
            return m_Captures
                .OrderBy(entry => entry.Capture.Sequence)
                .Select(entry => entry.Result())
                .ToArray();
        }
    }

    public void Seal()
    {
        lock (m_Gate) m_Sealed = true;
    }

    private bool IsCompleteLocked() =>
        m_Sealed &&
        m_Captures.Count > 0 &&
        m_Captures.All(entry => entry.State is
            RuntimeVisualSummaryCaptureState.Succeeded or
            RuntimeVisualSummaryCaptureState.Failed);

    private CaptureEntry ResolveCapturingLocked(RuntimeVisualSummaryCapture capture)
    {
        CaptureEntry entry = ResolveLocked(capture);
        if (entry.State != RuntimeVisualSummaryCaptureState.Capturing)
        {
            throw new InvalidOperationException(
                $"Visual-summary capture '{capture.Name}' is not being captured.");
        }

        return entry;
    }

    private CaptureEntry ResolveLocked(RuntimeVisualSummaryCapture capture)
    {
        CaptureEntry? entry = m_Captures.FirstOrDefault(candidate =>
            candidate.Capture.Sequence == capture.Sequence &&
            candidate.Capture == capture);
        return entry ?? throw new InvalidOperationException(
            $"Visual-summary capture '{capture.Name}' is not registered.");
    }

    private void FailLocked(CaptureEntry entry, string message)
    {
        entry.State = RuntimeVisualSummaryCaptureState.Failed;
        entry.FailureMessage = message;
        Volatile.Write(ref m_FailureMessage, message);
    }

    private static string ValidateCaptureName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string canonical = name.Trim();
        if (canonical.Length > 64 || canonical.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException(
                "Visual-summary capture names may contain only ASCII letters, digits, '-' and '_'.",
                nameof(name));
        }

        return canonical;
    }

    private static string GetNamedOutputPath(string basePath, string name)
    {
        string directory = Path.GetDirectoryName(basePath) ?? Directory.GetCurrentDirectory();
        string stem = Path.GetFileNameWithoutExtension(basePath);
        string extension = Path.GetExtension(basePath);
        if (extension.Length == 0) extension = ".json";
        return Path.Combine(directory, $"{stem}.{name}{extension}");
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

    private sealed class CaptureEntry
    {
        public CaptureEntry(RuntimeVisualSummaryCapture capture)
        {
            Capture = capture;
        }

        public RuntimeVisualSummaryCapture Capture { get; }
        public RuntimeVisualSummaryCaptureState State { get; set; } =
            RuntimeVisualSummaryCaptureState.Scheduled;
        public string? FailureMessage { get; set; }

        public RuntimeVisualSummaryCaptureResult Result() =>
            new(Capture, State, FailureMessage);
    }
}
