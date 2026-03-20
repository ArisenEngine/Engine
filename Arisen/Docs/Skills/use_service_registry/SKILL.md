---
name: Consume or Provide Arisen Services
description: How to safely decouple Domain packages using the Arisen IServiceRegistry.
---

# Using the ServiceRegistry
When a user asks you to connect two major systems together (e.g., the Game Logic telling the Renderer what to do), you MUST use the `IServiceRegistry` pattern defined in `Docs/Architecture/ServiceRegistry.md`.

## 1. No Static Domain References
**CRITICAL:** A Domain Package (like `com.user.game`) MUST NEVER directly static-reference another Domain Package (like `com.arisen.rhi.vulkan`). If you generate code that casts a registry object back to its concrete implementation (e.g. `(VulkanDevice)registry.Get<IRHIDevice>()`), you have destroyed the Microkernel Architecture.

## 2. Providing a Service
If you write a backend subsystem:
1. Declare the intent via C# Interfaces defined in a Foundation package.
2. In `IPackageEntry.OnLoad()`, call `services.Register<IMyService>(new MyConcreteService());`
3. Ensure `package.json` declares `"services": { "provides": [ {"interface": "IMyService"} ] }`.

## 3. Consuming a Service
If you write a subsystem that requires a backend to function:
1. Ensure `package.json` declares `"services": { "requires": [ "IMyService" ] }` so the Kernel can validate load order.
2. In your `IEngineSubsystem.OnInit(IServiceRegistry registry)`, call `_myService = registry.Get<IMyService>();`.
3. **DO NOT cache the registry itself.** Cache the specific service instance you retrieved.
