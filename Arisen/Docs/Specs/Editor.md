# Editor Extensibility Spec

## UI Framework (Avalonia)
The Arisen Editor uses Avalonia UI to provide a cross-platform (Windows/Mac/Linux) native user interface.

- **Pattern**: Stick strictly to the **MVVM** (Model-View-ViewModel) Pattern.
- **Views**: Written in `.axaml` files. Do not put business logic in the code-behind (`.axaml.cs`). Code-behind is strictly for UI-specific events that cannot be handled via bindings.
- **ViewModels**: Inherit from `ReactiveObject` (from `ReactiveUI`) or `EditorPanelBase`. Use `this.RaiseAndSetIfChanged` for property notifications and `ReactiveCommand` for commands. Leverage `WhenAnyValue` for reactive logic.

## Editor vs Engine Separation
- The Editor is an overarching host process. The `Engine` is a dependency sandbox running inside it.
- **Logging**: The editor separates logs visually via `LogService`. Logs from the engine (`Player`) and logs from the editor (`Editor`) are interleaved but filtered. Always ensure `Logger.Information` calls provide the correct context if necessary.
- **No Blocking**: Engine simulation logic is heavy and multi-threaded. The UI thread must never wait synchronously for the Engine.

## Creating Editor Windows
1. Define the View (`MyCustomView.axaml`) with its data bindings.
2. Define the ViewModel (`MyCustomViewModel.cs`).
3. If it is a dockable tool, ensure it inherits the appropriate base doc-node class.
