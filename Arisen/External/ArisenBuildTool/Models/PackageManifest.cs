using System.Collections.Generic;

namespace ArisenBuildTool.Models;

public class PackageEntry
{
    public string? Assembly { get; set; }
}

public class PackageSubsystem
{
    public string Class { get; set; } = string.Empty;
    public string Phase { get; set; } = "Init";
    public int Priority { get; set; } = 100;
}

public class PackageServiceProvider
{
    public string Interface { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
}

public class PackageServices
{
    public List<PackageServiceProvider>? Provides { get; set; }
    public List<string>? Requires { get; set; }
}

public class PackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }
    
    // Legacy support, but better inferred from NativeRuntimes / Entry
    public string? Type { get; set; }

    public PackageEntry? Entry { get; set; }
    public PackageServices? Services { get; set; }
    public List<PackageSubsystem>? Subsystems { get; set; }
    
    public Dictionary<string, string>? Dependencies { get; set; }
    public Dictionary<string, string>? NugetDependencies { get; set; }
    public Dictionary<string, List<string>>? NativeRuntimes { get; set; }
}
