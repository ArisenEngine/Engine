---
name: create_editor_command
description: How to implement a user action (like adding a component or moving an entity) using a centralized Command Pattern instead of putting the logic directly into the .axaml.cs code-behind or a direct ViewModel method.
---

# Creating an Editor Command

To adhere to the **AI-First Architecture & Editor Automation** rules, all user actions in the Arisen Editor MUST be implemented as actionable Commands rather than direct logic in the UI or ViewModels. This ensures that an AI agent or a headless automation script can trigger the exact same actions without needing a UI.

## 1. Define the Command
Create a new command class that implements the `ICommand` interface. It should encapsulate all the necessary data to perform and undo the action.

```csharp
using ArisenEditor.Core.Commands;

public class ChangeEntityNameCommand : ICommand
{
    private readonly uint _entityId;
    private readonly string _newName;
    private readonly string _oldName;

    public ChangeEntityNameCommand(uint entityId, string oldName, string newName)
    {
        _entityId = entityId;
        _oldName = oldName;
        _newName = newName;
    }

    public void Execute()
    {
        // Engine Kernel/Scene interaction goes here
        // e.g. EngineKernel.Instance.GetSubsystem<SceneManager>().SetName(_entityId, _newName);
    }

    public void Undo()
    {
        // Revert the action
        // e.g. EngineKernel.Instance.GetSubsystem<SceneManager>().SetName(_entityId, _oldName);
    }
}
```

## 2. Execute via CommandManager
When the human clicks a button or an AI decides to rename an entity, they must create an instance of the command and pass it to the `CommandManager`.

```csharp
// Inside your ViewModel:
public void OnRenameEntity(uint entityId, string oldName, string newName)
{
    var command = new ChangeEntityNameCommand(entityId, oldName, newName);
    EditorApplication.Instance.CommandManager.Execute(command);
}
```

## Why this is AI-Friendly
- **Discoverability:** The AI can query the `CommandManager` or the type system to see all available actions it can perform.
- **Headless automation:** It bypassing Avalonia completely.
- **Safety:** Because commands can have `Undo()`, AI agents can try actions and revert them if they realize they made a mistake or if the user rejects the AI's proposal.

**NEVER** put the logic directly into `button_Click` event handlers or directly in the ViewModel's mutation methods.

