using System.Collections.Generic;

namespace ArisenKernel.Packages;

/// <summary>
/// Defines a required package and its potential remote source.
/// </summary>
public class PackageRequirement
{
    public string Id { get; set; } = string.Empty;
    public string? Url { get; set; } // https://github.com/... or file:///...
    public string? Version { get; set; }
}

/// <summary>
/// Defines the structure of an Arisen project manifest file (.arisen).
/// </summary>
public class ProjectManifest
{
    public string Name { get; set; } = "New Arisen Project";
    
    public string EngineVersion { get; set; } = Lifecycle.EngineVersion.Current.ToString();
    
    /// <summary>
    /// List of package requirements for this project.
    /// </summary>
    public List<PackageRequirement> Packages { get; set; } = new();
}

