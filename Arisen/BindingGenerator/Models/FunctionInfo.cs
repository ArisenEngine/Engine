namespace BindingGenerator.Models;

public record FunctionInfo(string ReturnType, string Name, List<(string Type, string Name)> Parameters);
