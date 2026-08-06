using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class GenericRenderPipelineFrameSubmissionTests
{
    [Fact]
    public void SuccessfulFanoutAllocatesNoManagedMemoryAfterWarmup()
    {
        var actions = new FaultingFrameSubmissionActions(
            preparedFailure: null,
            disposalFailure: null,
            featureFailure: null);
        var sequence = new FeatureSequence();
        var feature = new FaultingFeature(
            "feature.success",
            sequence,
            failure: null);
        IGenericRenderPipelineFeature[] features = [feature];
        var context = new GenericRenderPipelineFeatureSubmissionContext(
            submittedTicket: 61);

        for (int i = 0; i < 64; i++)
        {
            GenericRenderPipelineFrameSubmission.Execute(
                ref actions,
                submittedTicket: 61);
            GenericRenderPipelineFeatureDispatcher.OnFrameSubmitted(
                features,
                context);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++)
        {
            GenericRenderPipelineFrameSubmission.Execute(
                ref actions,
                submittedTicket: 61);
            GenericRenderPipelineFeatureDispatcher.OnFrameSubmitted(
                features,
                context);
        }
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
        Assert.Equal(61ul, actions.PreparedTicket);
        Assert.Equal(61ul, actions.FeatureTicket);
        Assert.Equal(61ul, feature.SubmittedTicket);
    }

    [Fact]
    public void CoreParticipantFailuresDoNotPreemptLaterTicketOwners()
    {
        var preparedFailure = new InvalidOperationException("prepared failed");
        var disposalFailure = new InvalidOperationException("disposal failed");
        var featureFailure = new InvalidOperationException("features failed");
        var actions = new FaultingFrameSubmissionActions(
            preparedFailure,
            disposalFailure,
            featureFailure);

        var thrown = Assert.Throws<AggregateException>(() =>
            GenericRenderPipelineFrameSubmission.Execute(
                ref actions,
                submittedTicket: 71));

        Assert.Equal(1, actions.PreparedSequence);
        Assert.Equal(2, actions.DisposalSequence);
        Assert.Equal(3, actions.FeatureSequence);
        Assert.Equal(71ul, actions.PreparedTicket);
        Assert.Equal(71ul, actions.FeatureTicket);
        Assert.Equal(3, thrown.InnerExceptions.Count);
        Assert.Same(preparedFailure, thrown.InnerExceptions[0]);
        AssertAttributedFailure(
            thrown.InnerExceptions[1],
            "Generic RP frame submission completed-resource release failed.",
            disposalFailure);
        AssertAttributedFailure(
            thrown.InnerExceptions[2],
            "Generic RP frame submission feature notification failed.",
            featureFailure);
    }

    [Fact]
    public void SoleCoreFailureIsPreservedAfterLaterParticipantsRun()
    {
        var disposalFailure = new InvalidOperationException("disposal failed");
        var actions = new FaultingFrameSubmissionActions(
            preparedFailure: null,
            disposalFailure,
            featureFailure: null);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            GenericRenderPipelineFrameSubmission.Execute(
                ref actions,
                submittedTicket: 81));

        Assert.Same(disposalFailure, thrown);
        Assert.Equal(1, actions.PreparedSequence);
        Assert.Equal(2, actions.DisposalSequence);
        Assert.Equal(3, actions.FeatureSequence);
        Assert.Equal(81ul, actions.PreparedTicket);
        Assert.Equal(81ul, actions.FeatureTicket);
    }

    [Fact]
    public void MultipleFeatureFailuresAreAttributedWithoutStoppingFanout()
    {
        var sequence = new FeatureSequence();
        var firstFailure = new InvalidOperationException("first failed");
        var thirdFailure = new InvalidOperationException("third failed");
        var first = new FaultingFeature("feature.first", sequence, firstFailure);
        var second = new FaultingFeature("feature.second", sequence, failure: null);
        var third = new FaultingFeature("feature.third", sequence, thirdFailure);
        IGenericRenderPipelineFeature[] features = [first, second, third];
        var context = new GenericRenderPipelineFeatureSubmissionContext(
            submittedTicket: 91);

        var thrown = Assert.Throws<AggregateException>(() =>
            GenericRenderPipelineFeatureDispatcher.OnFrameSubmitted(
                features,
                context));

        Assert.Equal(1, first.CallSequence);
        Assert.Equal(2, second.CallSequence);
        Assert.Equal(3, third.CallSequence);
        Assert.Equal(91ul, first.SubmittedTicket);
        Assert.Equal(91ul, second.SubmittedTicket);
        Assert.Equal(91ul, third.SubmittedTicket);
        Assert.Equal(2, thrown.InnerExceptions.Count);
        AssertFeatureFailure(
            thrown.InnerExceptions[0],
            first.FeatureId,
            firstFailure);
        AssertFeatureFailure(
            thrown.InnerExceptions[1],
            third.FeatureId,
            thirdFailure);
    }

    [Fact]
    public void SoleFeatureFailurePreservesExistingHookDiagnostic()
    {
        var sequence = new FeatureSequence();
        var firstFailure = new InvalidOperationException("first failed");
        var first = new FaultingFeature("feature.first", sequence, firstFailure);
        var second = new FaultingFeature("feature.second", sequence, failure: null);
        IGenericRenderPipelineFeature[] features = [first, second];
        var context = new GenericRenderPipelineFeatureSubmissionContext(
            submittedTicket: 101);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            GenericRenderPipelineFeatureDispatcher.OnFrameSubmitted(
                features,
                context));

        AssertFeatureFailure(thrown, first.FeatureId, firstFailure);
        Assert.Equal(1, first.CallSequence);
        Assert.Equal(2, second.CallSequence);
        Assert.Equal(101ul, second.SubmittedTicket);
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

    private static void AssertFeatureFailure(
        Exception failure,
        string featureId,
        Exception expectedInner)
    {
        var attributed = Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(
            $"[GenericRP.Features] Feature '{featureId}' failed during OnFrameSubmitted.",
            attributed.Message);
        Assert.Same(expectedInner, attributed.InnerException);
    }

    private struct FaultingFrameSubmissionActions :
        IGenericRenderPipelineFrameSubmissionActions
    {
        private readonly Exception? m_PreparedFailure;
        private readonly Exception? m_DisposalFailure;
        private readonly Exception? m_FeatureFailure;
        private int m_NextSequence;

        public FaultingFrameSubmissionActions(
            Exception? preparedFailure,
            Exception? disposalFailure,
            Exception? featureFailure)
        {
            m_PreparedFailure = preparedFailure;
            m_DisposalFailure = disposalFailure;
            m_FeatureFailure = featureFailure;
            m_NextSequence = 0;
            PreparedSequence = 0;
            DisposalSequence = 0;
            FeatureSequence = 0;
            PreparedTicket = 0;
            FeatureTicket = 0;
        }

        public int PreparedSequence { get; private set; }
        public int DisposalSequence { get; private set; }
        public int FeatureSequence { get; private set; }
        public ulong PreparedTicket { get; private set; }
        public ulong FeatureTicket { get; private set; }

        public void UpdatePreparedAssetTicket(ulong submittedTicket)
        {
            PreparedSequence = ++m_NextSequence;
            PreparedTicket = submittedTicket;
            if (m_PreparedFailure != null)
            {
                throw m_PreparedFailure;
            }
        }

        public void ReleaseCompletedResources()
        {
            DisposalSequence = ++m_NextSequence;
            if (m_DisposalFailure != null)
            {
                throw m_DisposalFailure;
            }
        }

        public void NotifyFeatures(ulong submittedTicket)
        {
            FeatureSequence = ++m_NextSequence;
            FeatureTicket = submittedTicket;
            if (m_FeatureFailure != null)
            {
                throw m_FeatureFailure;
            }
        }
    }

    private sealed class FeatureSequence
    {
        public int Value;
    }

    private sealed class FaultingFeature : IGenericRenderPipelineFeature
    {
        private readonly FeatureSequence m_Sequence;
        private readonly Exception? m_Failure;

        public FaultingFeature(
            string featureId,
            FeatureSequence sequence,
            Exception? failure)
        {
            FeatureId = featureId;
            m_Sequence = sequence;
            m_Failure = failure;
        }

        public string FeatureId { get; }
        public int Order => 0;
        public int CallSequence { get; private set; }
        public ulong SubmittedTicket { get; private set; }

        public void ConsumeExtractedFrame(
            in GenericRenderPipelineFeatureFrameContext context)
        {
        }

        public void PrepareResources(
            in GenericRenderPipelineFeatureFrameContext context)
        {
        }

        public void AddRenderGraphPasses(
            GenericRenderPipelineFeatureGraphStage stage,
            in GenericRenderPipelineFeatureGraphContext context)
        {
        }

        public void OnFrameSubmitted(
            in GenericRenderPipelineFeatureSubmissionContext context)
        {
            CallSequence = ++m_Sequence.Value;
            SubmittedTicket = context.SubmittedTicket;
            if (m_Failure != null)
            {
                throw m_Failure;
            }
        }

        public void ReleaseDeviceResources()
        {
        }
    }
}
