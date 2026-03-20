using System;

namespace ArisenEngine.Core.Automation;

/// <summary>
/// Universal automation manager resolving undo/redo across the entire engine.
/// Decoupled from any Editor UI frameworks.
/// </summary>
public interface ICommandManager
{
    void Execute(ICommand command);
    void Undo();
    void Redo();
    void Clear();
    
    bool CanUndo { get; }
    bool CanRedo { get; }
    int MaxHistorySize { get; set; }

    event Action<ICommand>? CommandExecuted;
    event Action<ICommand>? CommandUndone;
    event Action<ICommand>? CommandRedone;
    
    /// <summary>
    /// Fired when CanUndo or CanRedo property values change.
    /// Useful for MVVM frameworks to subscribe and update UI bindings.
    /// </summary>
    event Action? StateChanged;
}
