using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ArisenBuildTool.Models;

public class PackageEntry
{
    [JsonPropertyName("assembly")]
    public string? Assembly { get; set; }
    
    [JsonPropertyName("class")]
    public string? Class { get; set; }
}

public class PackageSubsystem
{
    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;
    
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "Init";
    
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;
}

public class PackageServiceProvider
{
    [JsonPropertyName("interface")]
    public string Interface { get; set; } = string.Empty;
    
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;
}

public class PackageServices
{
    [JsonPropertyName("provides")]
    public List<PackageServiceProvider>? Provides { get; set; }
    
    [JsonPropertyName("requires")]
    public List<string>? Requires { get; set; }
}

public class PackageManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonPropertyName("author")]
    public string? Author { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("entry")]
    public PackageEntry? Entry { get; set; }
    
    [JsonPropertyName("services")]
    public PackageServices? Services { get; set; }
    
    [JsonPropertyName("subsystems")]
    public List<PackageSubsystem>? Subsystems { get; set; }
    
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
    
    [JsonPropertyName("nugetDependencies")]
    public Dictionary<string, string>? NugetDependencies { get; set; }
    
    [JsonPropertyName("nativeRuntimes")]
    public Dictionary<string, List<string>>? NativeRuntimes { get; set; }
}
