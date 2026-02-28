namespace ArisenEngine.Core.Graph;

public readonly struct GraphEdge : IEquatable<GraphEdge>
{
    public uint SourceNodeId { get; }
    public int SourcePortIndex { get; }
    public uint TargetNodeId { get; }
    public int TargetPortIndex { get; }

    public GraphEdge(uint sourceNodeId, int sourcePortIndex, uint targetNodeId, int targetPortIndex)
    {
        SourceNodeId = sourceNodeId;
        SourcePortIndex = sourcePortIndex;
        TargetNodeId = targetNodeId;
        TargetPortIndex = targetPortIndex;
    }

    public bool Equals(GraphEdge other)
    {
        return SourceNodeId == other.SourceNodeId &&
               SourcePortIndex == other.SourcePortIndex &&
               TargetNodeId == other.TargetNodeId &&
               TargetPortIndex == other.TargetPortIndex;
    }

    public override bool Equals(object? obj) => obj is GraphEdge other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(SourceNodeId, SourcePortIndex, TargetNodeId, TargetPortIndex);

    public static bool operator ==(GraphEdge left, GraphEdge right) => left.Equals(right);
    public static bool operator !=(GraphEdge left, GraphEdge right) => !left.Equals(right);
}