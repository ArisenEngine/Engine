---
name: use-service-registry
description: Guides safe decoupling of packages via IServiceRegistry. Use when connecting subsystems or providing/consuming engine services.
---

# Service Registry SOP

Use `IServiceRegistry` to decouple packages while preserving lifecycle control and package boundaries.

Reference architecture doc:
- `Arisen/Docs/Architecture/ServiceRegistry.md`

## 1. Safety checklist
- [ ] No static domain references between disconnected packages.
- [ ] Register and consume interfaces, not concrete implementations.
- [ ] Do not cast resolved services back to concrete types.
- [ ] Cache the resolved service instance, not the registry itself.
- [ ] Do not use service lookups inside hot inner loops.
- [ ] Distinguish package selection from service consumption: composition/root packages may depend on concrete providers; reusable domain packages should consume shared contracts.

## 2. Providing a service
If a package provides a new engine service:
1. [ ] Declare the interface in the appropriate shared/foundation package.
2. [ ] Implement the interface inside the provider package.
3. [ ] Register it from `IPackageEntry.OnLoad(IServiceRegistry services)`.
4. [ ] Declare the provided service in `package.json` metadata when the package contract requires it.
5. [ ] If registration is intentionally delayed until backend initialization, mark the provider declaration with `deferred: true` and document the initialization point.

Example:
```csharp
services.Register<IMyService>(new MyConcreteService());
```

## 3. Consuming a service
If a package depends on another service:
1. [ ] Declare the required service in `package.json` when applicable.
2. [ ] Resolve it during subsystem or package initialization, not repeatedly in hot paths.
3. [ ] Store the interface reference for later use.
4. [ ] Use service `capabilities` when the consumer requires a specific backend feature, such as `{ "interface": "ArisenKernel.Contracts.IRHIBackend", "capabilities": ["vulkan"] }`.

Example:
```csharp
_myService = registry.Get<IMyService>();
```

## 4. Verification
- Verify both provider and consumer packages are present in the active workspace manifest.
- Verify a concrete provider is selected by the workspace manifest or by a composition/root package dependency; a service requirement alone does not add a package to the graph.
- Verify lifecycle ordering makes the provider available before the consumer uses it.
- Verify the final usage path stays outside hot inner loops.
- For runtime/RHI changes, run `Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` when local graphics prerequisites are available.
