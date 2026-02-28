namespace ArisenEngine.Core.Graph;

public enum PortDirection { Input, Output }
public enum PortCapacity { Single, Multi }

public sealed class PortDefinition
{
    public string Name { get; }
    public Type DataType { get; }
    public PortDirection Direction { get; }
    public PortCapacity Capacity { get; }

    public PortDefinition(string name, Type dataType, PortDirection direction, PortCapacity capacity = PortCapacity.Multi)
    {
        Name = name;
        DataType = dataType;
        Direction = direction;
        Capacity = capacity;
    }
}
