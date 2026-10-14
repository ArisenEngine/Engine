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
    /// Package-owned world descriptor selected for streaming-capable runtime composition.
    /// StartupScene remains the compatibility bootstrap until world streaming owns activation.
    /// </summary>
    public ProjectAssetReference? StartupWorld { get; set; }

    /// <summary>
    /// Package-owned render-pipeline settings asset selected for this workspace.
    /// </summary>
    public ProjectAssetReference? RenderPipeline { get; set; }

    /// <summary>
    /// Package-owned sample catalogue selected for this workspace. A profile that selects a sample
    /// by id (`--startup-sample`) and a hub that browses the gallery both resolve through it.
    /// </summary>
    public ProjectAssetReference? SampleCatalogue { get; set; }

    /// <summary>
    /// Profile declarations. A profile may select the startup target of its own product; profiles
    /// that declare nothing boot the workspace-level <see cref="StartupScene"/>/<see cref="StartupWorld"/>.
    /// </summary>
    public Dictionary<string, ProfileDefinition>? Profiles { get; set; }

    /// <summary>
    /// List of package requirements for this project.
    /// </summary>
    public List<PackageRequirement> Packages { get; set; } = new();
}

/// <summary>
/// The startup target a profile's product boots with. A profile declares at most one target
/// (scene, world, or sample id); the process selects it by profile instead of editing the
/// workspace-level startup identity.
/// </summary>
public class ProfileDefinition
{
    public ProjectAssetReference? StartupScene { get; set; }

    public ProjectAssetReference? StartupWorld { get; set; }

    /// <summary>
    /// Id of a catalogue entry the profile boots, resolved through the workspace's sample catalogue.
    /// </summary>
    public string? StartupSample { get; set; }

    /// <summary>
    /// True when this profile hosts the sample gallery: it boots the hub, can switch between the
    /// catalogue's content-only samples in-process, and its cook closes over the whole catalogue.
    /// </summary>
    public bool IsSampleGallery { get; set; }
}

