namespace ArisenEngine.Core.Graph;

public abstract class GraphNode
{
    public uint Id { get; internal set; }
    public string Name { get; set; } = string.Empty;

    private readonly List<PortDefinition> m_InputPorts = new();
    private readonly List<PortDefinition> m_OutputPorts = new();

    public IReadOnlyList<PortDefinition> InputPorts => m_InputPorts;
    public IReadOnlyList<PortDefinition> OutputPorts => m_OutputPorts;

    protected void AddInputPort(string name, Type dataType, PortCapacity capacity = PortCapacity.Multi)
    {
        m_InputPorts.Add(new PortDefinition(name, dataType, PortDirection.Input, capacity));
    }

    protected void AddOutputPort(string name, Type dataType, PortCapacity capacity = PortCapacity.Multi)
    {
        m_OutputPorts.Add(new PortDefinition(name, dataType, PortDirection.Output, capacity));
    }
}
