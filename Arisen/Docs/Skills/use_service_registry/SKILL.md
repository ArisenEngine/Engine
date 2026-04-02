---
name: use-service-registry
description: Guides safe decoupling of packages via IServiceRegistry. Use when connecting subsystems or providing/consuming engine services.
---

# Service Registry SOP

Use the `IServiceRegistry` pattern to decoupling packages. This allows the Arisen Microkernel to manage dependencies and lifecycle.

## 1. Safety Checklist
- [ ] **NO Static Domain References**: A Domain package must never directly reference another Domain package.
- [ ] **No Concrete Casts**: Use interfaces only. Never cast a service to its concrete implementation.
- [ ] **No Repository Caching**: Cache the service instance, not the registry itself.

## 2. Providing a Service
If you are implementing a new engine service (e.g., a new RHI backend):
1. [ ] **Declare Interface**: Definitions MUST live in a Foundation package (e.g., `com.arisen.rhi`).
2. [ ] **Implementation**: Implement the interface in your package.
3. [ ] **Registration**: In `IPackageEntry.OnLoad(IServiceRegistry services)`:
    ```csharp
    services.Register<IMyService>(new MyConcreteService());
    ```
4. [ ] **Metadata**: Declare `"services": { "provides": [ "IMyService" ] }` in `package.json`.

## 3. Consuming a Service
If your subsystem requires another service (e.g., the Game logic needs the Renderer):
1. [ ] **Declare Dependency**: Add the interface ID to `"services": { "requires": [ "IMyService" ] }` in `package.json`.
2. [ ] **Retrieval**: In your `IEngineSubsystem.OnInit(IServiceRegistry registry)`:
    ```csharp
    _myService = registry.Get<IMyService>();
    ```
3. [ ] **Null Checks**: Ensure the service exists before use, or handle the missing service gracefully.

## 4. Verification
- Verify that both provider and consumer packages appear in the `manifest.json`.
- Check that the Kernel's service log reports successful registration and resolution.
