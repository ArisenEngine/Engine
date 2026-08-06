using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderFramePostSubmissionTests
{
    private const string RenderingPackageDirectory =
        "Arisen/Development/PackageGame/Local/com.arisen.rendering";

    [Fact]
    public void SuccessfulCompletionAllocatesNoManagedMemoryAfterWarmup()
    {
        var actions = new FaultingPostSubmissionActions(
            notificationFailure: null,
            readbackFailure: null);

        for (int i = 0; i < 64; i++)
        {
            RenderFramePostSubmission.Execute(
                ref actions,
                submittedTicket: 31);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++)
        {
            RenderFramePostSubmission.Execute(
                ref actions,
                submittedTicket: 31);
        }
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
        Assert.Equal(31ul, actions.NotifiedTicket);
        Assert.Equal(31ul, actions.ReadbackTicket);
        Assert.Equal(0, actions.AbortSequence);
    }

    [Fact]
    public void ReadbackFailureCannotPreemptSubmissionNotification()
    {
        var readbackFailure = new InvalidOperationException("readback failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure: null,
            readbackFailure);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            RenderFramePostSubmission.Execute(ref actions, submittedTicket: 41));

        Assert.Same(readbackFailure, thrown);
        Assert.Equal(1, actions.NotificationSequence);
        Assert.Equal(2, actions.ReadbackSequence);
        Assert.Equal(41ul, actions.NotifiedTicket);
        Assert.Equal(41ul, actions.ReadbackTicket);
    }

    [Fact]
    public void NotificationFailureStillCompletesReadback()
    {
        var notificationFailure = new InvalidOperationException("notification failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure,
            readbackFailure: null);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            RenderFramePostSubmission.Execute(ref actions, submittedTicket: 51));

        Assert.Same(notificationFailure, thrown);
        Assert.Equal(1, actions.NotificationSequence);
        Assert.Equal(2, actions.ReadbackSequence);
        Assert.Equal(51ul, actions.NotifiedTicket);
        Assert.Equal(51ul, actions.ReadbackTicket);
    }

    [Fact]
    public void DualFailurePreservesNotificationAndAttributesReadback()
    {
        var notificationFailure = new InvalidOperationException("notification failed");
        var readbackFailure = new InvalidOperationException("readback failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure,
            readbackFailure);

        var thrown = Assert.Throws<AggregateException>(() =>
            RenderFramePostSubmission.Execute(ref actions, submittedTicket: 61));

        Assert.Equal(1, actions.NotificationSequence);
        Assert.Equal(2, actions.ReadbackSequence);
        Assert.Equal(2, thrown.InnerExceptions.Count);
        Assert.Same(notificationFailure, thrown.InnerExceptions[0]);
        var attributedReadback = Assert.IsType<InvalidOperationException>(
            thrown.InnerExceptions[1]);
        Assert.Equal(
            "Render frame visual readback completion failed.",
            attributedReadback.Message);
        Assert.Same(readbackFailure, attributedReadback.InnerException);
    }

    [Fact]
    public void LaterExecutionFailureNotifiesAcceptedTicketAndAbortsReadback()
    {
        var executionFailure = new InvalidOperationException("second submit failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure: null,
            readbackFailure: null);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            RenderFramePostSubmission.ThrowExecutionFailure(
                ref actions,
                ticketBeforeExecution: 0,
                acceptedTicket: 71,
                executionFailure));

        Assert.Same(executionFailure, thrown);
        Assert.Equal(1, actions.NotificationSequence);
        Assert.Equal(0, actions.ReadbackSequence);
        Assert.Equal(2, actions.AbortSequence);
        Assert.Equal(71ul, actions.NotifiedTicket);
    }

    [Fact]
    public void ReleaseFailureRemainsPrimaryWhenNotificationAndAbortFail()
    {
        var executionFailure = new InvalidOperationException(
            "command-buffer release failed");
        var notificationFailure = new InvalidOperationException(
            "notification failed");
        var abortFailure = new InvalidOperationException("abort failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure,
            readbackFailure: null,
            abortFailure);

        var thrown = Assert.Throws<AggregateException>(() =>
            RenderFramePostSubmission.ThrowExecutionFailure(
                ref actions,
                ticketBeforeExecution: 0,
                acceptedTicket: 81,
                executionFailure));

        Assert.Equal(3, thrown.InnerExceptions.Count);
        Assert.Same(executionFailure, thrown.InnerExceptions[0]);
        AssertAttributedFailure(
            thrown.InnerExceptions[1],
            "Render frame submission notification failed.",
            notificationFailure);
        AssertAttributedFailure(
            thrown.InnerExceptions[2],
            "Render frame visual readback cancellation failed.",
            abortFailure);
        Assert.Equal(1, actions.NotificationSequence);
        Assert.Equal(0, actions.ReadbackSequence);
        Assert.Equal(2, actions.AbortSequence);
    }

    [Fact]
    public void FailureBeforeAnySubmitSkipsNotificationAndAbortsReadback()
    {
        var executionFailure = new InvalidOperationException("recording failed");
        var actions = new FaultingPostSubmissionActions(
            notificationFailure: null,
            readbackFailure: null);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            RenderFramePostSubmission.ThrowExecutionFailure(
                ref actions,
                ticketBeforeExecution: 0,
                acceptedTicket: 0,
                executionFailure));

        Assert.Same(executionFailure, thrown);
        Assert.Equal(0, actions.NotificationSequence);
        Assert.Equal(0, actions.ReadbackSequence);
        Assert.Equal(1, actions.AbortSequence);
    }

    [Fact]
    public void ProductionRenderGraphCommitsAcceptedTicketBeforeReset()
    {
        string source = ReadRenderingSource("RenderGraph.cs");
        int executeIndex = source.IndexOf(
            "public ulong Execute(RenderContext context)",
            StringComparison.Ordinal);
        int ticketSnapshotIndex = source.IndexOf(
            "ulong ticketBeforeExecution = context.Submission.LastTicket;",
            executeIndex,
            StringComparison.Ordinal);
        int finallyIndex = source.IndexOf(
            "finally",
            ticketSnapshotIndex,
            StringComparison.Ordinal);
        int commitIndex = source.IndexOf(
            "RenderGraphSubmissionTicketTracker.CommitAcceptedTicket(",
            finallyIndex,
            StringComparison.Ordinal);
        int graphTicketIndex = source.IndexOf(
            "ref m_LastSubmittedTicket,",
            commitIndex,
            StringComparison.Ordinal);
        int beforeTicketIndex = source.IndexOf(
            "ticketBeforeExecution,",
            graphTicketIndex,
            StringComparison.Ordinal);
        int currentTicketIndex = source.IndexOf(
            "context.Submission.LastTicket);",
            beforeTicketIndex,
            StringComparison.Ordinal);
        int resetIndex = source.IndexOf(
            "Reset();",
            currentTicketIndex,
            StringComparison.Ordinal);

        Assert.True(executeIndex >= 0);
        Assert.True(ticketSnapshotIndex > executeIndex);
        Assert.True(finallyIndex > ticketSnapshotIndex);
        Assert.True(commitIndex > finallyIndex);
        Assert.True(graphTicketIndex > commitIndex);
        Assert.True(beforeTicketIndex > graphTicketIndex);
        Assert.True(currentTicketIndex > beforeTicketIndex);
        Assert.True(resetIndex > currentTicketIndex);
    }

    [Fact]
    public void ProductionPipelineRoutesGraphExecutionFailureToExceptionalActions()
    {
        string source = ReadRenderingSource("RenderPipeline.cs");
        int executeIndex = source.IndexOf(
            "submittedTicket = m_RenderGraph.Execute(context);",
            StringComparison.Ordinal);
        int catchIndex = source.IndexOf(
            "catch (Exception executionFailure)",
            executeIndex,
            StringComparison.Ordinal);
        int exceptionalHandlingIndex = source.IndexOf(
            "RenderFramePostSubmission.ThrowExecutionFailure(",
            catchIndex,
            StringComparison.Ordinal);
        int beforeTicketIndex = source.IndexOf(
            "ticketBeforeExecution,",
            exceptionalHandlingIndex,
            StringComparison.Ordinal);
        int acceptedTicketIndex = source.IndexOf(
            "context.Submission.LastTicket,",
            beforeTicketIndex,
            StringComparison.Ordinal);
        int executionFailureIndex = source.IndexOf(
            "executionFailure);",
            acceptedTicketIndex,
            StringComparison.Ordinal);
        int successfulHandlingIndex = source.IndexOf(
            "RenderFramePostSubmission.Execute(ref postSubmissionActions, submittedTicket);",
            executionFailureIndex,
            StringComparison.Ordinal);

        Assert.True(executeIndex >= 0);
        Assert.True(catchIndex > executeIndex);
        Assert.True(exceptionalHandlingIndex > catchIndex);
        Assert.True(beforeTicketIndex > exceptionalHandlingIndex);
        Assert.True(acceptedTicketIndex > beforeTicketIndex);
        Assert.True(executionFailureIndex > acceptedTicketIndex);
        Assert.True(successfulHandlingIndex > executionFailureIndex);
        Assert.Contains(
            "m_Pipeline.OnFrameSubmitted(m_Context, submittedTicket);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_Pipeline.m_OutputReadbackPass?.Abort(executionFailure);",
            source,
            StringComparison.Ordinal);
    }

    private static void AssertAttributedFailure(
        Exception failure,
        string expectedMessage,
        Exception expectedInner)
    {
        var attributed = Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(expectedMessage, attributed.Message);
        Assert.Same(expectedInner, attributed.InnerException);
    }

    private static string ReadRenderingSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            CppSourceContractScanner.FindRepoRoot(),
            RenderingPackageDirectory,
            fileName));
    }

    private struct FaultingPostSubmissionActions : IRenderFramePostSubmissionActions
    {
        private readonly Exception? m_NotificationFailure;
        private readonly Exception? m_ReadbackFailure;
        private readonly Exception? m_AbortFailure;
        private int m_NextSequence;

        public FaultingPostSubmissionActions(
            Exception? notificationFailure,
            Exception? readbackFailure,
            Exception? abortFailure = null)
        {
            m_NotificationFailure = notificationFailure;
            m_ReadbackFailure = readbackFailure;
            m_AbortFailure = abortFailure;
            m_NextSequence = 0;
            NotificationSequence = 0;
            ReadbackSequence = 0;
            AbortSequence = 0;
            NotifiedTicket = 0;
            ReadbackTicket = 0;
        }

        public int NotificationSequence { get; private set; }
        public int ReadbackSequence { get; private set; }
        public int AbortSequence { get; private set; }
        public ulong NotifiedTicket { get; private set; }
        public ulong ReadbackTicket { get; private set; }

        public void NotifyFrameSubmitted(ulong submittedTicket)
        {
            NotificationSequence = ++m_NextSequence;
            NotifiedTicket = submittedTicket;
            if (m_NotificationFailure != null)
            {
                throw m_NotificationFailure;
            }
        }

        public void CompleteReadback(ulong submittedTicket)
        {
            ReadbackSequence = ++m_NextSequence;
            ReadbackTicket = submittedTicket;
            if (m_ReadbackFailure != null)
            {
                throw m_ReadbackFailure;
            }
        }

        public void AbortReadback(Exception executionFailure)
        {
            ArgumentNullException.ThrowIfNull(executionFailure);
            AbortSequence = ++m_NextSequence;
            if (m_AbortFailure != null)
            {
                throw m_AbortFailure;
            }
        }
    }
}
