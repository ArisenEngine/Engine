namespace BindingGenerator.Models;

public record StructInfo(string Name, List<(string Type, string Name)> Fields);
