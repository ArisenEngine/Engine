using System.Reflection;

namespace ArisenEngine.Core.Packages;

public class ArisenPackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public PackageSource Source { get; set; } = PackageSource.Builtin;
    public string EngineVersion { get; set; } = string.Empty;
    public Dictionary<string, string> Dependencies { get; set; } = new();
    public Assembly? Assembly { get; set; }
    public object? EntryInstance { get; set; }
}
