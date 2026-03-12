using System;
using ArisenEngine.Core.Serialization;

namespace ArisenEngine.Core.Assets;

/// <summary>
/// Serializable metadata associated with project assets to track stable references across files.
/// Typically serialized to a .meta file alongside the main file.
/// </summary>
public class AssetMetadata : ISerializationCallbackReceiver
{
    /// <summary>
    /// A globally unique identifier used by the AssetDatabase to safely reference this asset.
    /// </summary>
    public Guid Guid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The detected file extension or generic asset type at the time of import.
    /// </summary>
    public string AssetType { get; set; } = string.Empty;

    public void OnAfterDeserialize()
    {
    }

    public void OnBeforeSerialize()
    {
    }
}
