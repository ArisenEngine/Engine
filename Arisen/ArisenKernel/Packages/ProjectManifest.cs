using System;
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
/// Identifies a package-owned asset selected by workspace-level project settings.
/// </summary>
public class ProjectAssetReference
{
    public Guid Guid { get; set; }

    public string PackageId { get; set; } = string.Empty;

    public bool IsValid => Guid != Guid.Empty && !string.IsNullOrWhiteSpace(PackageId);
}

/// <summary>
/// Defines the structure of an Arisen project manifest file (.arisen).
/// </summary>
public class ProjectManifest
{
    public string Name { get; set; } = "New Arisen Project";
    
    public string EngineVersion { get; set; } = Lifecycle.EngineVersion.Current.ToString();

    /// <summary>
    /// Scene asset activated before the first engine frame.
    /// </summary>
    public ProjectAssetReference? StartupScene { get; set; }

    /// <summary>
    /// Package-owned render-pipeline settings asset selected for this workspace.
    /// </summary>
    public ProjectAssetReference? RenderPipeline { get; set; }
    
    /// <summary>
    /// List of package requirements for this project.
    /// </summary>
    public List<PackageRequirement> Packages { get; set; } = new();
}

