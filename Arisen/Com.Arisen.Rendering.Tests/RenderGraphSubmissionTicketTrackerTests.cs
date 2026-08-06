using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderGraphSubmissionTicketTrackerTests
{
    [Fact]
    public void FirstAcceptedTicketCommitsWhenLaterExecutionFails()
    {
        ulong graphTicket = 31;

        bool committed = RenderGraphSubmissionTicketTracker.CommitAcceptedTicket(
            ref graphTicket,
            ticketBeforeExecution: 0,
            ticketAfterExecution: 41);

        Assert.True(committed);
        Assert.Equal(41ul, graphTicket);
    }

    [Fact]
    public void AcceptedTicketCommitsBeforePostSubmitReleaseFailureEscapes()
    {
        ulong graphTicket = 51;

        bool committed = RenderGraphSubmissionTicketTracker.CommitAcceptedTicket(
            ref graphTicket,
            ticketBeforeExecution: 0,
            ticketAfterExecution: 61);

        Assert.True(committed);
        Assert.Equal(61ul, graphTicket);
    }

    [Fact]
    public void FailureBeforeSubmissionDoesNotRepublishPriorTicket()
    {
        ulong graphTicket = 71;

        bool committed = RenderGraphSubmissionTicketTracker.CommitAcceptedTicket(
            ref graphTicket,
            ticketBeforeExecution: 0,
            ticketAfterExecution: 0);

        Assert.False(committed);
        Assert.Equal(71ul, graphTicket);
    }
}
