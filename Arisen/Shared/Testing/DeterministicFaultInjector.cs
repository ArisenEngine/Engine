namespace Arisen.Testing;

public sealed class DeterministicFaultInjector<TStage>
    where TStage : notnull
{
    private readonly object m_Gate = new();
    private readonly Dictionary<TStage, Func<Exception>> m_Pending = new();
    private readonly List<TStage> m_Triggered = new();

    public void Arm(TStage stage, Func<Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);

        lock (m_Gate)
        {
            if (m_Pending.ContainsKey(stage) || m_Triggered.Contains(stage))
            {
                throw new InvalidOperationException(
                    $"Fault stage '{stage}' is already pending or has already triggered.");
            }

            m_Pending.Add(stage, exceptionFactory);
        }
    }

    public void ThrowIfArmed(TStage stage)
    {
        Func<Exception>? exceptionFactory;
        lock (m_Gate)
        {
            if (!m_Pending.Remove(stage, out exceptionFactory))
            {
                return;
            }

            m_Triggered.Add(stage);
        }

        Exception exception = exceptionFactory();
        if (exception == null)
        {
            throw new InvalidOperationException(
                $"Fault stage '{stage}' returned no exception.");
        }

        throw exception;
    }

    public DeterministicFaultSnapshot<TStage> Snapshot()
    {
        lock (m_Gate)
        {
            return new DeterministicFaultSnapshot<TStage>(
                m_Pending.Keys.ToArray(),
                m_Triggered.ToArray());
        }
    }

    public void EnsureFullyConsumed()
    {
        TStage[] pending;
        lock (m_Gate)
        {
            pending = m_Pending.Keys.ToArray();
        }

        if (pending.Length != 0)
        {
            throw new InvalidOperationException(
                $"Fault stages were not consumed: {string.Join(", ", pending)}.");
        }
    }

    public void Reset()
    {
        lock (m_Gate)
        {
            m_Pending.Clear();
            m_Triggered.Clear();
        }
    }
}

public sealed class DeterministicFaultSnapshot<TStage>
    where TStage : notnull
{
    internal DeterministicFaultSnapshot(TStage[] pendingStages, TStage[] triggeredStages)
    {
        PendingStages = Array.AsReadOnly(pendingStages);
        TriggeredStages = Array.AsReadOnly(triggeredStages);
    }

    public IReadOnlyList<TStage> PendingStages { get; }
    public IReadOnlyList<TStage> TriggeredStages { get; }
}
