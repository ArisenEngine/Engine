namespace ArisenEngine.Core.Graph;

public interface IGraphExecutionContext
{
    // Context data for node execution (e.g. results, parameters)
}

public interface IGraphExecutionPolicy<TNode> where TNode : GraphNode
{
    void Execute(TNode node, IGraphExecutionContext? context);
    Task ExecuteAsync(TNode node, IGraphExecutionContext? context);
}

public sealed class GraphExecutor<TNode> where TNode : GraphNode
{
    /// <summary>
    /// Executes nodes one by one in topological order.
    /// </summary>
    public void ExecuteSequential(CompiledGraph<TNode> compiled, IGraphExecutionPolicy<TNode> policy,
        IGraphExecutionContext? context = null)
    {
        foreach (var node in compiled.SortedNodes)
        {
            policy.Execute(node, context);
        }
    }

    /// <summary>
    /// Executes nodes in parallel layer by layer.
    /// Nodes in the same layer are executed concurrently using Task.Run.
    /// </summary>
    public async Task ExecuteParallelAsync(CompiledGraph<TNode> compiled, IGraphExecutionPolicy<TNode> policy,
        IGraphExecutionContext? context = null)
    {
        foreach (var layer in compiled.ParallelLayers)
        {
            var tasks = new Task[layer.Count];
            for (int i = 0; i < layer.Count; i++)
            {
                var node = layer[i];
                tasks[i] = Task.Run(() => policy.Execute(node, context));
            }

            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Executes nodes in parallel layer by layer using policy's Async methods.
    /// </summary>
    public async Task ExecuteParallelCustomAsync(CompiledGraph<TNode> compiled, IGraphExecutionPolicy<TNode> policy,
        IGraphExecutionContext? context = null)
    {
        foreach (var layer in compiled.ParallelLayers)
        {
            var tasks = new Task[layer.Count];
            for (int i = 0; i < layer.Count; i++)
            {
                tasks[i] = policy.ExecuteAsync(layer[i], context);
            }

            await Task.WhenAll(tasks);
        }
    }
}