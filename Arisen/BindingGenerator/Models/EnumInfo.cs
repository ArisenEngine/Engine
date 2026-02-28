namespace BindingGenerator.Models;

public record EnumInfo(string Name, string BaseType, List<(string Name, string? Value)> Values);