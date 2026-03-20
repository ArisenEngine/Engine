namespace ArisenEngine.Core.Automation;

/// <summary>
/// Universal interface for all undoable engine and editor commands.
/// Every user action MUST be implemented as an ICommand to support
/// undo/redo and headless automation (AI-First Architecture).
/// </summary>
public interface ICommand
{
    /// <summary>
    /// A human-readable description of what this command does (for logging and UI histories).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command, applying its changes.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverts the changes made by Execute().
    /// </summary>
    void Undo();
}
