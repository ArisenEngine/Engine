namespace ArisenEngine.Core.Graph;

public class Graph<TNode> where TNode : GraphNode
{
    private readonly Dictionary<uint, TNode> m_Nodes = new();
    private readonly List<GraphEdge> m_Edges = new();
    private uint m_NextNodeId = 1;

    public IReadOnlyCollection<TNode> Nodes => m_Nodes.Values;
    public IReadOnlyList<GraphEdge> Edges => m_Edges;

    public TNode AddNode(TNode node)
    {
        node.Id = m_NextNodeId++;
        m_Nodes.Add(node.Id, node);
        return node;
    }

    public bool RemoveNode(uint nodeId)
    {
        if (m_Nodes.Remove(nodeId))
        {
            m_Edges.RemoveAll(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
            return true;
        }
        return false;
    }

    public TNode? GetNode(uint nodeId)
    {
        return m_Nodes.TryGetValue(nodeId, out var node) ? node : null;
    }

    public void Connect(uint srcNodeId, int srcPortIndex, uint dstNodeId, int dstPortIndex)
    {
        if (!m_Nodes.TryGetValue(srcNodeId, out var srcNode))
            throw new ArgumentException($"Source node {srcNodeId} not found");
        if (!m_Nodes.TryGetValue(dstNodeId, out var dstNode))
            throw new ArgumentException($"Destination node {dstNodeId} not found");

        if (srcPortIndex < 0 || srcPortIndex >= srcNode.OutputPorts.Count)
            throw new ArgumentOutOfRangeException(nameof(srcPortIndex));
        if (dstPortIndex < 0 || dstPortIndex >= dstNode.InputPorts.Count)
            throw new ArgumentOutOfRangeException(nameof(dstPortIndex));

        // Check if destination port is Single and already connected
        var dstPortDef = dstNode.InputPorts[dstPortIndex];
        if (dstPortDef.Capacity == PortCapacity.Single)
        {
            if (m_Edges.Any(e => e.TargetNodeId == dstNodeId && e.TargetPortIndex == dstPortIndex))
                throw new InvalidOperationException($"Input port {dstPortIndex} of node {dstNodeId} is already connected and has Single capacity");
        }

        var edge = new GraphEdge(srcNodeId, srcPortIndex, dstNodeId, dstPortIndex);
        if (!m_Edges.Contains(edge))
        {
            m_Edges.Add(edge);
        }
    }

    public void Disconnect(GraphEdge edge)
    {
        m_Edges.Remove(edge);
    }
}
