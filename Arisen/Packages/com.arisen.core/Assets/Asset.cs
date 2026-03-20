using System;
using ArisenEngine.Core.Serialization;

namespace ArisenEngine.Core.Assets;

/// <summary>
/// Base class for all engine assets that can be serialized and managed.
/// </summary>
public abstract class Asset : ISerializationCallbackReceiver
{
    /// <summary>
    /// Unique identifier for the asset.
    /// </summary>
    public Guid Guid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Friendly name of the asset.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public virtual void OnBeforeSerialize() { }
    public virtual void OnAfterDeserialize() { }
}
