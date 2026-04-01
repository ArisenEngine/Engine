using ArisenEngine.Threading;
using ArisenKernel.Diagnostics;
using System.Threading;

namespace PackageGame;

public static class TaskGraphTest
{
    public static void RunTest(ITaskGraph graph)
    {
        KernelLog.Info("[TaskGraphTest] Starting dependency test...");

        // Define tasks
        var t1 = new ActionTask(() => {
            KernelLog.Info("[TaskGraphTest] Task 1: Starting (2s)...");
            Thread.Sleep(2000);
            KernelLog.Info("[TaskGraphTest] Task 1: Completed.");
        }, "Task1");

        var t2 = new ActionTask(() => {
            KernelLog.Info("[TaskGraphTest] Task 2: Starting (1s)...");
            Thread.Sleep(1000);
            KernelLog.Info("[TaskGraphTest] Task 2: Completed.");
        }, "Task2");

        var t3 = new ActionTask(() => {
            KernelLog.Info("[TaskGraphTest] Task 3: (Dependent on 1 and 2) starting...");
            KernelLog.Info("[TaskGraphTest] Task 3: Completed.");
        }, "Task3");

        // Add to graph
        graph.AddTask(t1);
        graph.AddTask(t2);
        graph.AddTask(t3);

        // Define dependencies: T3 depends on T1 and T2
        graph.AddDependency(t1, t3);
        graph.AddDependency(t2, t3);

        // Execute
        KernelLog.Info("[TaskGraphTest] Executing graph...");
        graph.Execute();
        KernelLog.Info("[TaskGraphTest] Graph execution finished successfully.");
    }
}
