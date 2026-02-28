using System.Collections.Generic;
using System.Linq;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.Core.Lifecycle;

internal sealed class FrameScheduler
{
    internal void ExecuteFrame(float deltaTime, IEnumerable<IEngineSubsystem> subsystems)
    {
        using var _ = Profiler.Zone("FrameScheduler.ExecuteFrame");
        // Simple tick for all ITickableSubsystem. 
        // Once JobSystem is added, this will schedule jobs using a DAG.
        foreach (var subsystem in subsystems.OfType<ITickableSubsystem>())
        {
            subsystem.Tick(deltaTime);
        }
        
        // TODO: Frame end flush, deferred actions, etc.
    }
}
