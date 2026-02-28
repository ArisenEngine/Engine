namespace BindingGenerator.Models;

public record BridgeBlock(string ClassName, string DllName, string Namespace, List<FunctionInfo> Functions);
