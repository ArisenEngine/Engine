namespace ArisenKernel.Lifecycle;

public readonly record struct RuntimeSmokeScenarioContext(
    string ModeName,
    string WorkspacePath,
    string ProfileName,
    string? OutputPath,
    IRuntimeVisualSummaryService? VisualSummaryService);

public interface IRuntimeSmokeScenarioProvider
{
    bool TryCreateScenario(
        RuntimeSmokeScenarioContext context,
        out IRuntimeSmokeScenario scenario,
        out string diagnostic);
}

public interface IRuntimeSmokeScenario
{
    string Name { get; }
    string OutputPath { get; }
    bool IsReadyForShutdown { get; }
    bool IsComplete { get; }
    bool Succeeded { get; }
    string? FailureMessage { get; }

    void Start(uint initialFrameIndex);
    void BeforeFrame(uint frameIndex);
    void AfterFrame(uint frameIndex);
    void ReportFailure(string message);
    void AfterShutdown();
}
