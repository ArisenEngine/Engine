using System.Collections.Generic;
using System.Linq;

namespace ArisenKernel.Lifecycle;

internal sealed class FrameScheduler
{
    // P1: Cache tickable subsystems to avoid LINQ OfType<> allocation every frame
    private List<ITickableSubsystem>? m_CachedTickables;

    /// <summary>
    /// Call after subsystem registration is complete to build the cached tick list.
    /// </summary>
    internal void BuildTickCache(IEnumerable<IEngineSubsystem> subsystems)
    {
        m_CachedTickables = subsystems.OfType<ITickableSubsystem>().ToList();
    }

    internal void ExecuteFrame(float deltaTime, IEnumerable<IEngineSubsystem> subsystems)
    {
        // P1: Use cached list instead of per-frame LINQ allocation
        if (m_CachedTickables == null)
        {
            BuildTickCache(subsystems);
        }

        for (int i = 0; i < m_CachedTickables!.Count; i++)
        {
            m_CachedTickables[i].Tick(deltaTime);
        }

        // TODO: Frame end flush, deferred actions, etc.
    }
}
