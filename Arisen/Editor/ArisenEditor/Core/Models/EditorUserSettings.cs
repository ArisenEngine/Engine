using System;

namespace ArisenEditor.Core.Models;

/// <summary>
/// Represents local, user-specific editor layout and preferences for a given project.
/// This file normally resides in the `Library/` directory and is NOT version controlled.
/// </summary>
public class EditorUserSettings
{
    /// <summary>
    /// The Guid of the scene that was last open before the editor closed.
    /// Loaded automatically if valid during project synthesis.
    /// </summary>
    public Guid LastOpenedSceneGuid { get; set; } = Guid.Empty;
}
