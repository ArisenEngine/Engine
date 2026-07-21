using System;
using System.Threading;
using ArisenEngine.Core.ECS;
using ArisenEngine.Threading;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TaskGraphExecutionTests
{
    [Fact]
    public void OneShotGraph_HonorsDependencies()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        int predecessorCount = 0;
        int observedPredecessorCount = 0;

        var first = new ActionTask(
            () => Interlocked.Increment(ref predecessorCount),
            "FirstPredecessor");
        var second = new ActionTask(
            () => Interlocked.Increment(ref predecessorCount),
            "SecondPredecessor");
        var dependent = new ActionTask(
            () => observedPredecessorCount = Volatile.Read(ref predecessorCount),
            "Dependent");

        taskGraph.AddTask(first);
        taskGraph.AddTask(second);
        taskGraph.AddTask(dependent);
        taskGraph.AddDependency(first, dependent);
        taskGraph.AddDependency(second, dependent);

        taskGraph.Execute();

        Assert.Equal(2, predecessorCount);
        Assert.Equal(2, observedPredecessorCount);
    }

    [Fact]
    public void ReusableSchedule_ExecutesEveryTickInDependencyOrder()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        int producerExecutions = 0;
        int consumerExecutions = 0;
        int orderingFailures = 0;

        var producer = new ActionTask(
            () => Interlocked.Increment(ref producerExecutions),
            "Producer");
        var consumer = new ActionTask(
            () =>
            {
                int consumerFrame = Interlocked.Increment(ref consumerExecutions);
                if (Volatile.Read(ref producerExecutions) != consumerFrame)
                {
                    Interlocked.Increment(ref orderingFailures);
                }
            },
            "Consumer");

        using var schedule = taskGraph.CreateSchedule(
            new TaskNode[] { producer, consumer },
            new[] { new TaskDependency(producer, consumer) });

        Assert.Equal(2, schedule.TaskCount);
        Assert.Equal(2, schedule.LayerCount);

        for (int frame = 0; frame < 64; frame++)
        {
            schedule.Execute();
        }

        Assert.Equal(64, producerExecutions);
        Assert.Equal(64, consumerExecutions);
        Assert.Equal(0, orderingFailures);
    }

    [Fact]
    public void WorkerFailure_PropagatesAndDoesNotContaminateNextOneShotGraph()
    {
        using var taskGraph = new TaskGraph(workerCount: 1);
        var failing = new ActionTask(
            () => throw new TestTaskException("expected failure"),
            "FailingTask");
        taskGraph.AddTask(failing);

        var exception = Assert.Throws<InvalidOperationException>(() => taskGraph.Execute());

        Assert.Contains("FailingTask", exception.Message, StringComparison.Ordinal);
        Assert.IsType<TestTaskException>(exception.InnerException);

        int successfulExecutions = 0;
        taskGraph.AddTask(new ActionTask(
            () => successfulExecutions++,
            "RecoveryTask"));

        taskGraph.Execute();

        Assert.Equal(1, successfulExecutions);
    }

    [Fact]
    public void BackgroundTask_RunsWithoutBlockingCallerAndReturnsResult()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        using var started = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        BackgroundTask<int> background = taskGraph.Schedule(
            "DelayedBackgroundRead",
            cancellationToken =>
            {
                started.Set();
                release.Wait(cancellationToken);
                return 42;
            });

        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        Assert.False(background.IsCompleted);
        Assert.Equal(1, taskGraph.OutstandingTaskCount);

        int foregroundExecutions = 0;
        taskGraph.AddTask(new ActionTask(() => foregroundExecutions++, "ForegroundGraph"));
        taskGraph.Execute();
        Assert.Equal(1, foregroundExecutions);

        release.Set();
        Assert.True(background.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(BackgroundTaskStatus.Succeeded, background.Status);
        Assert.True(background.TryGetResult(out int result));
        Assert.Equal(42, result);
        Assert.Equal(0, taskGraph.OutstandingTaskCount);
    }

    [Fact]
    public void BackgroundTask_CancellationAndFailureAreObservableAndIsolated()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        using var started = new ManualResetEventSlim(false);
        BackgroundTask<int> cancelled = taskGraph.Schedule(
            "CancellableBackgroundRead",
            cancellationToken =>
            {
                started.Set();
                cancellationToken.WaitHandle.WaitOne();
                cancellationToken.ThrowIfCancellationRequested();
                return 1;
            });
        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        cancelled.Cancel();
        Assert.True(cancelled.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(BackgroundTaskStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.TryGetResult(out _));

        BackgroundTask<int> failed = taskGraph.Schedule<int>(
            "FailingBackgroundRead",
            _ => throw new TestTaskException("background failure"));
        Assert.True(failed.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(BackgroundTaskStatus.Failed, failed.Status);
        Assert.IsType<TestTaskException>(failed.Failure);

        BackgroundTask<int> recovered = taskGraph.Schedule(
            "RecoveredBackgroundRead",
            _ => 7);
        Assert.True(recovered.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(recovered.TryGetResult(out int result));
        Assert.Equal(7, result);
    }

    [Fact]
    public void SystemContainer_ExecutesSystemsOnEveryFrameAndHonorsComponentHazards()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        using var systems = new SystemContainer(taskGraph);
        var entityManager = new EntityManager();
        var entity = entityManager.CreateEntity();
        entityManager.AddComponent(entity, new SchedulerCounterComponent());
        var producer = new CounterProducerSystem();
        var consumer = new CounterConsumerSystem();
        systems.AddSystem(producer);
        systems.AddSystem(consumer);

        for (int frame = 0; frame < 32; frame++)
        {
            systems.Execute(entityManager, 1.0f / 60.0f);
            Assert.Equal(frame + 1, consumer.LastObservedValue);
        }

        Assert.Equal(32, producer.ExecutionCount);
        Assert.Equal(32, consumer.ExecutionCount);
        Assert.Equal(32, entityManager.GetComponent<SchedulerCounterComponent>(entity).Value);
    }

    [Fact]
    public void FailedSystemFrame_DiscardsDeferredStructuralCommands()
    {
        using var taskGraph = new TaskGraph(workerCount: 1);
        using var systems = new SystemContainer(taskGraph);
        var entityManager = new EntityManager();
        var entity = entityManager.CreateEntity();
        var system = new DeferredFailureSystem(entity);
        systems.AddSystem(system);

        Assert.Throws<InvalidOperationException>(
            () => systems.Execute(entityManager, 1.0f / 60.0f));
        Assert.False(entityManager.HasComponent<DeferredMarkerComponent>(entity));

        system.QueueMutation = false;
        system.ThrowAfterQueue = false;
        systems.Execute(entityManager, 1.0f / 60.0f);

        Assert.False(entityManager.HasComponent<DeferredMarkerComponent>(entity));
    }

    [Fact]
    public void FailedCommandPlayback_DiscardsUnplayedCommandBuffers()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        using var systems = new SystemContainer(taskGraph);
        var entityManager = new EntityManager();
        var entity = entityManager.CreateEntity();
        var invalidSystem = new InvalidDeferredCommandSystem();
        var markerSystem = new DeferredMarkerSystem(entity);
        systems.AddSystem(invalidSystem);
        systems.AddSystem(markerSystem);

        Assert.Throws<InvalidOperationException>(
            () => systems.Execute(entityManager, 1.0f / 60.0f));
        Assert.False(entityManager.HasComponent<DeferredMarkerComponent>(entity));

        invalidSystem.QueueInvalidCommand = false;
        markerSystem.QueueMutation = false;
        systems.Execute(entityManager, 1.0f / 60.0f);

        Assert.False(entityManager.HasComponent<DeferredMarkerComponent>(entity));
    }

    private sealed class TestTaskException : Exception
    {
        public TestTaskException(string message)
            : base(message)
        {
        }
    }

    private struct SchedulerCounterComponent : IComponent
    {
        public int Value;
    }

    private struct DeferredMarkerComponent : IComponent
    {
        public int Value;
    }

    [WriteComponent(typeof(SchedulerCounterComponent))]
    private sealed class CounterProducerSystem : ISystem
    {
        public string Name => nameof(CounterProducerSystem);
        public int ExecutionCount { get; private set; }

        public void Execute(EntityManager em, EntityCommandBuffer ecb, float dt)
        {
            var components = em.GetPool<SchedulerCounterComponent>().GetRawComponentArray();
            components[0].Value++;
            ExecutionCount++;
        }
    }

    [Fact]
    public void CancelledBackgroundTaskDisposesCompletedUnclaimedResult()
    {
        using var taskGraph = new TaskGraph(workerCount: 2);
        using var started = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var result = new DisposableBackgroundResult();
        BackgroundTask<DisposableBackgroundResult> task = taskGraph.Schedule(
            "DisposableCancellation",
            _ =>
            {
                started.Set();
                release.Wait();
                return result;
            });

        Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
        task.Cancel();
        release.Set();
        Assert.True(task.Wait(TimeSpan.FromSeconds(2)));

        Assert.Equal(BackgroundTaskStatus.Cancelled, task.Status);
        Assert.True(result.IsDisposed);
        Assert.False(task.TryGetResult(out _));
    }

    private sealed class DisposableBackgroundResult : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [ReadComponent(typeof(SchedulerCounterComponent))]
    private sealed class CounterConsumerSystem : ISystem
    {
        public string Name => nameof(CounterConsumerSystem);
        public int ExecutionCount { get; private set; }
        public int LastObservedValue { get; private set; }

        public void Execute(EntityManager em, EntityCommandBuffer ecb, float dt)
        {
            LastObservedValue = em.GetPool<SchedulerCounterComponent>().GetRawComponentArray()[0].Value;
            ExecutionCount++;
        }
    }

    private sealed class DeferredFailureSystem : ISystem
    {
        private readonly Entity m_Entity;

        public string Name => nameof(DeferredFailureSystem);
        public bool QueueMutation { get; set; } = true;
        public bool ThrowAfterQueue { get; set; } = true;

        public DeferredFailureSystem(Entity entity)
        {
            m_Entity = entity;
        }

        public void Execute(EntityManager em, EntityCommandBuffer ecb, float dt)
        {
            if (QueueMutation)
            {
                ecb.AddComponent(m_Entity, new DeferredMarkerComponent { Value = 1 });
            }

            if (ThrowAfterQueue)
            {
                throw new TestTaskException("system frame failed");
            }
        }
    }

    private sealed class InvalidDeferredCommandSystem : ISystem
    {
        public string Name => nameof(InvalidDeferredCommandSystem);
        public bool QueueInvalidCommand { get; set; } = true;

        public void Execute(EntityManager em, EntityCommandBuffer ecb, float dt)
        {
            if (QueueInvalidCommand)
            {
                ecb.AddComponent(Entity.Null, new DeferredMarkerComponent { Value = 1 });
            }
        }
    }

    private sealed class DeferredMarkerSystem : ISystem
    {
        private readonly Entity m_Entity;

        public string Name => nameof(DeferredMarkerSystem);
        public bool QueueMutation { get; set; } = true;

        public DeferredMarkerSystem(Entity entity)
        {
            m_Entity = entity;
        }

        public void Execute(EntityManager em, EntityCommandBuffer ecb, float dt)
        {
            if (QueueMutation)
            {
                ecb.AddComponent(m_Entity, new DeferredMarkerComponent { Value = 1 });
            }
        }
    }
}
