using System;
using System.IO;
using YamlDotNet.Serialization;

namespace ArisenEditor.Core.Assets;

/// <summary>
/// Represents the serialized data in a .meta file.
/// </summary>
public class AssetMetadata
{
    [YamlMember(Alias = "guid")]
    public Guid Guid { get; set; }
    
    [YamlMember(Alias = "importer_type")]
    public string? ImporterType { get; set; }
    
    // Future expansion: dependencies, importer settings, etc.
}
