using ArisenEngine.Rendering;
using ArisenKernel.Contracts;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class GraphicsDeviceLifecycleTests
{
    [Fact]
    public async Task RestartUnwindsAndRestoresParticipantsAroundBackendRecreation()
    {
        var events = new List<string>();
        ulong generation = 4;
        var coordinator = new GraphicsDeviceLifecycleCoordinator(
            (options, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                events.Add("backend");
                generation++;
                return Task.FromResult(generation);
            },
            () => generation);
        coordinator.RegisterParticipant(new Participant("provider", 200, events));
        coordinator.RegisterParticipant(new Participant("viewport", 100, events));

        GraphicsDeviceRestartResult result = await coordinator.RestartAsync(
            new RHIBackendRestartOptions(RHIBackendDiagnosticMode.RenderDoc));

        Assert.True(result.Succeeded);
        Assert.Equal((ulong)4, result.PreviousGeneration);
        Assert.Equal((ulong)5, result.CurrentGeneration);
        Assert.Equal(
            new[]
            {
                "prepare:viewport",
                "prepare:provider",
                "backend",
                "restore:provider",
                "restore:viewport"
            },
            events);
        Assert.Equal(GraphicsDeviceLifecycleState.Running, coordinator.Snapshot.State);
        Assert.Equal((ulong)5, coordinator.Snapshot.Generation);
        Assert.Equal(RHIBackendDiagnosticMode.RenderDoc, coordinator.Snapshot.DiagnosticMode);
    }

    [Fact]
    public async Task PrepareFailurePreventsBackendRecreationAndEntersFailStopState()
    {
        bool executorCalled = false;
        var coordinator = new GraphicsDeviceLifecycleCoordinator(
            (options, cancellationToken) =>
            {
                executorCalled = true;
                return Task.FromResult((ulong)3);
            },
            () => 2);
        coordinator.RegisterParticipant(new Participant(
            "failing",
            0,
            new List<string>(),
            prepareException: new InvalidOperationException("release failed")));

        GraphicsDeviceRestartResult result = await coordinator.RestartAsync(
            RHIBackendRestartOptions.Default);

        Assert.False(result.Succeeded);
        Assert.False(executorCalled);
        Assert.Equal(GraphicsDeviceLifecycleState.Failed, coordinator.Snapshot.State);
        Assert.Contains("release failed", result.Diagnostic, StringComparison.Ordinal);

        GraphicsDeviceRestartResult retry = await coordinator.RestartAsync(
            RHIBackendRestartOptions.Default);
        Assert.False(retry.Succeeded);
        Assert.Contains("fail-stop", retry.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonAdvancingBackendGenerationPreventsParticipantRestore()
    {
        var events = new List<string>();
        var coordinator = new GraphicsDeviceLifecycleCoordinator(
            (options, cancellationToken) => Task.FromResult((ulong)9),
            () => 9);
        coordinator.RegisterParticipant(new Participant("viewport", 0, events));

        GraphicsDeviceRestartResult result = await coordinator.RestartAsync(
            RHIBackendRestartOptions.Default);

        Assert.False(result.Succeeded);
        Assert.Equal(new[] { "prepare:viewport" }, events);
        Assert.Equal(GraphicsDeviceLifecycleState.Failed, coordinator.Snapshot.State);
        Assert.Contains("did not advance", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentRestartRequestIsRejectedWithoutJoiningActiveTransition()
    {
        ulong generation = 12;
        var enteredPrepare = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePrepare = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new GraphicsDeviceLifecycleCoordinator(
            (options, cancellationToken) =>
            {
                generation++;
                return Task.FromResult(generation);
            },
            () => generation);
        coordinator.RegisterParticipant(new Participant(
            "blocking",
            0,
            new List<string>(),
            prepare: async cancellationToken =>
            {
                enteredPrepare.TrySetResult(true);
                await releasePrepare.Task.WaitAsync(cancellationToken);
            }));

        Task<GraphicsDeviceRestartResult> active = coordinator.RestartAsync(
            RHIBackendRestartOptions.Default);
        await enteredPrepare.Task;

        GraphicsDeviceRestartResult concurrent = await coordinator.RestartAsync(
            RHIBackendRestartOptions.Default);
        Assert.False(concurrent.Succeeded);
        Assert.Contains("already active", concurrent.Diagnostic, StringComparison.OrdinalIgnoreCase);

        releasePrepare.TrySetResult(true);
        Assert.True((await active).Succeeded);
    }

    private sealed class Participant : IGraphicsDeviceLifecycleParticipant
    {
        private readonly List<string> m_Events;
        private readonly Exception? m_PrepareException;
        private readonly Func<CancellationToken, Task>? m_Prepare;

        public Participant(
            string participantId,
            int order,
            List<string> events,
            Exception? prepareException = null,
            Func<CancellationToken, Task>? prepare = null)
        {
            ParticipantId = participantId;
            Order = order;
            m_Events = events;
            m_PrepareException = prepareException;
            m_Prepare = prepare;
        }

        public string ParticipantId { get; }

        public int Order { get; }

        public async Task PrepareForGraphicsDeviceRestartAsync(
            GraphicsDeviceRestartContext context,
            CancellationToken cancellationToken)
        {
            m_Events.Add($"prepare:{ParticipantId}");
            if (m_PrepareException != null)
            {
                throw m_PrepareException;
            }
            if (m_Prepare != null)
            {
                await m_Prepare(cancellationToken);
            }
        }

        public Task RestoreAfterGraphicsDeviceRestartAsync(
            GraphicsDeviceRestartContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            m_Events.Add($"restore:{ParticipantId}");
            return Task.CompletedTask;
        }
    }
}
