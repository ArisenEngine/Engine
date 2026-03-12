# Subsystems Spec

Arisen Engine is built as a collection of modular `IEngineSubsystem` instances managed by the `EngineKernel`.

## 1. Lifecycle Interfaces
- **`IEngineSubsystem`**: Base interface for all modules. Requires `Initialize` and `Shutdown`.
- **`ITickableSubsystem`**: Extends the base to receive a `Tick(deltaTime)` call every frame.

## 2. Initialization Phases
Subsystems specify when they should boot via the `EnginePhase` enum:
- `PreInit`: Hardware-independent setup (Logging).
- `Init`: Hardware setup (RHI, JobSystem).
- `PostInit`: Content setup (World, Assets).

## 3. Priority and Ordering
- **Boot Order**: Subsystems are initialized in ascending order of their `Priority` property.
- **Shutdown Order**: Subsystems are shut down in **REVERSE** order of their initialization to ensure dependencies are released safely.

## 4. Accessing Subsystems
Use `EngineKernel.Instance.GetSubsystem<T>()` to find a specific module. Avoid tight coupling by using interfaces where possible.
