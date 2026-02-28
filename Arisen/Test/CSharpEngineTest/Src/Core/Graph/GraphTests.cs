using ArisenEngine.Core.Graph;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.Core.Graph;

public class GraphTests : ITest
{
    private class TestNode : GraphNode
    {
        public bool Executed { get; set; }
        public int ExecutionOrder { get; set; }

        public TestNode(string name)
        {
            Name = name;
            AddInputPort("In", typeof(int));
            AddOutputPort("Out", typeof(int));
        }
    }

    private class TestExecutionPolicy : IGraphExecutionPolicy<TestNode>
    {
        private int m_Counter = 0;

        public void Execute(TestNode node, IGraphExecutionContext? context)
        {
            node.Executed = true;
            node.ExecutionOrder = Interlocked.Increment(ref m_Counter);
            Logger.Log($"Executed node: {node.Name} (Order: {node.ExecutionOrder})");
        }

        public Task ExecuteAsync(TestNode node, IGraphExecutionContext? context)
        {
            Execute(node, context);
            return Task.CompletedTask;
        }
    }

    public string GetName() => "GraphSystem Core Tests";
    public TestCategory GetCategory() => TestCategory.Framework; // Assuming Framework fits

    public bool Setup() => true;

    public void Teardown()
    {
    }

    public bool Run()
    {
        return TestTopologicalSort() && TestCycleDetection() && TestParallelExecution();
    }

    private bool TestTopologicalSort()
    {
        Logger.Log("Testing Topological Sort...");
        var graph = new Graph<TestNode>();
        var a = graph.AddNode(new TestNode("A"));
        var b = graph.AddNode(new TestNode("B"));
        var c = graph.AddNode(new TestNode("C"));
        var d = graph.AddNode(new TestNode("D"));

        // A -> B -> D
        // A -> C -> D
        graph.Connect(a.Id, 0, b.Id, 0);
        graph.Connect(b.Id, 0, d.Id, 0);
        graph.Connect(a.Id, 0, c.Id, 0);
        graph.Connect(c.Id, 0, d.Id, 0);

        var compiled = GraphCompiler.Compile(graph);
        var executor = new GraphExecutor<TestNode>();
        var policy = new TestExecutionPolicy();

        executor.ExecuteSequential(compiled, policy);

        bool success = a.ExecutionOrder < b.ExecutionOrder &&
                       a.ExecutionOrder < c.ExecutionOrder &&
                       b.ExecutionOrder < d.ExecutionOrder &&
                       c.ExecutionOrder < d.ExecutionOrder;

        if (!success) Logger.Error("Topological sort order failed!");
        return success;
    }

    private bool TestCycleDetection()
    {
        Logger.Log("Testing Cycle Detection...");
        var graph = new Graph<TestNode>();
        var a = graph.AddNode(new TestNode("A"));
        var b = graph.AddNode(new TestNode("B"));

        graph.Connect(a.Id, 0, b.Id, 0);
        graph.Connect(b.Id, 0, a.Id, 0);

        bool hasCycle = GraphCompiler.HasCycle(graph);
        if (!hasCycle) Logger.Error("Cycle detection failed - should have detected a cycle!");

        try
        {
            GraphCompiler.Compile(graph);
            Logger.Error("Compiler should have thrown exception for cyclic graph");
            return false;
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        return hasCycle;
    }

    private bool TestParallelExecution()
    {
        Logger.Log("Testing Parallel Execution Layers...");
        var graph = new Graph<TestNode>();
        var root = graph.AddNode(new TestNode("Root"));

        // Root -> [A, B, C, D] -> End
        var nodes = new List<TestNode>();
        for (int i = 0; i < 4; i++) nodes.Add(graph.AddNode(new TestNode($"Parallel_{i}")));

        var end = graph.AddNode(new TestNode("End"));

        foreach (var n in nodes)
        {
            graph.Connect(root.Id, 0, n.Id, 0);
            graph.Connect(n.Id, 0, end.Id, 0);
        }

        var compiled = GraphCompiler.Compile(graph);
        if (compiled.ParallelLayers.Count != 3)
        {
            Logger.Error($"Expected 3 layers, got {compiled.ParallelLayers.Count}");
            return false;
        }

        if (compiled.ParallelLayers[1].Count != 4)
        {
            Logger.Error($"Expected 4 nodes in layer 1, got {compiled.ParallelLayers[1].Count}");
            return false;
        }

        return true;
    }
}