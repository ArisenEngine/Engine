# Architecture Spec: TaskGraph And Simulation Scheduling

**Status:** Active  
**Packages:** `com.arisen.taskgraph`, `com.arisen.ecs`

Arisen uses one package-owned worker pool for engine job execution. Consumers describe dependency topology or submit coarse background operations, while `TaskGraphPackage` owns worker creation and shutdown. Domain packages must not create private `TaskGraph` instances or private worker pools.

## Execution Modes

TaskGraph supports three deliberately separate modes:

1. **One-shot graph**
   - Build with `ITaskGraph.AddTask` and `AddDependency`.
   - Execute once with `ITaskGraph.Execute`.
   - The task/dependency graph is cleared after execution, including failed execution.
   - RenderGraph uses this mode because command-recording work items are rebuilt from the current frame.

2. **Reusable compiled schedule**
   - Build with `ITaskGraph.CreateSchedule` from a stable task list and dependency list.
   - Compilation produces immutable parallel layers.
   - `ITaskSchedule.Execute` can run the same topology every frame while task nodes receive updated frame context.
   - ECS `SystemContainer` uses this mode and recompiles only when systems change.

3. **Background operation**
   - Submit coarse, cancellation-aware work through `IBackgroundTaskScheduler.Schedule`.
   - `BackgroundTask<T>` exposes queued/running/succeeded/failed/cancelled state, failure, result, completion waiting, and a monotonic sequence.
   - Runtime package composition creates at least two workers and reserves one or two workers for background work. Foreground graph/schedule dispatch uses the remaining workers, so delayed file I/O cannot occupy every frame worker.
   - Cancellation is cooperative. Owners must cancel and drain their operations before the scheduler package unloads. If cancellation wins after an operation has returned an `IDisposable` result, `BackgroundTask<T>` disposes that unclaimed result before publishing `Cancelled`.
   - Background operations may produce immutable staging data. They must not mutate live ECS state, record RHI commands, invoke UI work, or publish callbacks that permit those mutations from a worker.

Do not change one-shot `Execute` to retain tasks, and do not rebuild a stable ECS topology every frame. Those are different lifetime contracts.

## Worker And Failure Policy

- `TaskGraphPackage` registers the same `TaskGraph` instance as `ITaskGraph` and `IBackgroundTaskScheduler` and is the only normal runtime owner of `TaskWorker` threads.
- Foreground graph work and background operations use fixed worker partitions. A directly constructed one-worker test executor cannot provide that isolation; normal package composition guarantees at least two workers.
- Work within one compiled layer may execute concurrently; the next layer starts only after every work item in the current layer signals completion.
- Normal dispatch uses value-type queue records and a reusable completion batch. It does not allocate delegate wrappers or completion events per task.
- Worker exceptions are captured with task context and rethrown on the waiting engine thread after the layer completes.
- One `TaskGraph` does not support concurrent `Execute` calls. Frame scheduling currently invokes simulation and RenderGraph recording sequentially against the shared executor.
- Disposing the shared executor while work is active is invalid. Package shutdown must happen after frame execution has stopped.
- Scheduler disposal cancels every outstanding background task, waits for the tracked set to drain, and only then joins worker threads.

## ECS Scheduling

`SceneSubsystem` resolves and caches `ITaskGraph` during initialization. `SystemContainer` creates one task node and one `EntityCommandBuffer` per registered system, then derives dependencies from:

- `ReadComponentAttribute`;
- `WriteComponentAttribute`;
- `ExecuteBeforeAttribute`;
- `ExecuteAfterAttribute`.

Conflicting component access follows system registration order unless explicit ordering says otherwise. Systems in independent layers may execute concurrently.

Before each simulation tick, `SystemContainer` updates task-node world and delta-time context, executes the reusable schedule, and then plays command buffers back sequentially in registration order. If any system fails, all command buffers are cleared and no deferred structural mutation from that frame is applied or retained for a later frame.

Command playback also clears each buffer in a `finally` path. If playback fails, `SystemContainer` clears every buffer that has not yet played before propagating the failure; commands applied by earlier buffers remain applied, but no stale command is replayed on the next frame.

## Hot-Path Rules

- Resolve the shared scheduler during subsystem initialization, not inside entity loops.
- Keep task topology compilation outside steady-state frame execution.
- Do not create worker threads, `CountdownEvent` instances, or delegate wrappers per frame.
- Keep structural ECS mutation deferred through per-system/per-thread command buffers.
- Keep background results immutable until their owning subsystem consumes them at an explicit frame boundary.
- Poll status or consume completion at a coarse owner boundary. Do not wait for background I/O from the simulation/render frame.
- Use Tracy zones at graph, layer, and task granularity; do not create per-entity zones.
- Add multi-frame tests for every persistent schedule consumer. A one-frame smoke cannot prove reusable execution semantics.

## Validation

Focused coverage lives in `Com.Arisen.Rendering.Tests/TaskGraphExecutionTests.cs` and verifies:

- one-shot dependency ordering;
- repeated reusable schedule execution;
- ECS component-hazard ordering across frames;
- worker failure propagation and one-shot graph recovery;
- system-execution and command-playback failure discard;
- background completion, failure, cancellation, shutdown drain, and foreground/background worker isolation.

Runtime/rendering changes must still pass:

```bat
Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1
```
