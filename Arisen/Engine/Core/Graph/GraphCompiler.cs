namespace ArisenEngine.Core.Graph;

public sealed class CompiledGraph<TNode> where TNode : GraphNode
{
    /// <summary>
    /// Nodes sorted by topological order.
    /// </summary>
    public IReadOnlyList<TNode> SortedNodes { get; }

    /// <summary>
    /// Nodes grouped by layers for parallel execution.
    /// Nodes in the same layer have no dependencies on each other.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<TNode>> ParallelLayers { get; }

    internal CompiledGraph(List<TNode> sortedNodes, List<List<TNode>> parallelLayers)
    {
        SortedNodes = sortedNodes;
        ParallelLayers = parallelLayers;
    }
}

public static class GraphCompiler
{
    public static CompiledGraph<TNode> Compile<TNode>(Graph<TNode> graph) where TNode : GraphNode
    {
        if (HasCycle(graph))
            throw new InvalidOperationException("Cannot compile a graph with cycles");

        var nodes = graph.Nodes.ToList();
        var edges = graph.Edges;

        // In-degree for each node
        var inDegree = nodes.ToDictionary(n => n.Id, n => 0);
        var adjacency = nodes.ToDictionary(n => n.Id, n => new List<uint>());

        foreach (var edge in edges)
        {
            inDegree[edge.TargetNodeId]++;
            adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        var sortedNodes = new List<TNode>();
        var parallelLayers = new List<List<TNode>>();

        // Queue for nodes with in-degree 0
        var currentLayerQueue = new Queue<TNode>(nodes.Where(n => inDegree[n.Id] == 0));

        while (currentLayerQueue.Count > 0)
        {
            var layer = new List<TNode>();
            var nextLayerQueue = new Queue<TNode>();

            while (currentLayerQueue.Count > 0)
            {
                var node = currentLayerQueue.Dequeue();
                layer.Add(node);
                sortedNodes.Add(node);

                foreach (var neighborId in adjacency[node.Id])
                {
                    inDegree[neighborId]--;
                    if (inDegree[neighborId] == 0)
                    {
                        nextLayerQueue.Enqueue(graph.Nodes.First(n => n.Id == neighborId));
                    }
                }
            }

            parallelLayers.Add(layer);
            currentLayerQueue = nextLayerQueue;
        }

        return new CompiledGraph<TNode>(sortedNodes, parallelLayers);
    }

    public static bool HasCycle<TNode>(Graph<TNode> graph) where TNode : GraphNode
    {
        var nodes = graph.Nodes.ToList();
        var visited = new HashSet<uint>();
        var recursionStack = new HashSet<uint>();
        var adjacency = nodes.ToDictionary(n => n.Id, n => new List<uint>());

        foreach (var edge in graph.Edges)
        {
            adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        foreach (var node in nodes)
        {
            if (IsCyclicUtil(node.Id, visited, recursionStack, adjacency))
                return true;
        }

        return false;
    }

    private static bool IsCyclicUtil(uint nodeId, HashSet<uint> visited, HashSet<uint> recursionStack, Dictionary<uint, List<uint>> adjacency)
    {
        if (recursionStack.Contains(nodeId)) return true;
        if (visited.Contains(nodeId)) return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);

        foreach (var neighborId in adjacency[nodeId])
        {
            if (IsCyclicUtil(neighborId, visited, recursionStack, adjacency))
                return true;
        }

        recursionStack.Remove(nodeId);
        return false;
    }
}
