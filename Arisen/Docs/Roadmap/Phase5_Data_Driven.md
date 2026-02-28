# Phase 5: The Data-Driven Synchronicity

**Objective**: Simultaneous and rigorous advancement of Engine logic and Editor tooling.

## 5.1 C# Entity Component System (ECS)
Move away from object-oriented MonoBehaviours to strict Archetype storage.

**Implementation Steps:**
1. **Chunk Memory & Archetypes:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Core\ECS\`.
   - Organize components into `64KB` (or similar size) unmanaged blocks. 
2. **System Jobs:**
   - Create `SystemBase` classes that iterate sequentially over Chunk array pointers via Job System.

## 5.2 Declarative Render Graph
Abstract explicit commands into a dependency solver.

**Implementation Steps:**
1. **RenderGraph API:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Rendering\Graph\RenderGraph.cs`.
   - Implement logic to declare passes, read/write resources, and automatically compile transit barriers.
2. **Transient Pools:**
   - Bind resource requirements dynamically per-frame out of a global GPU memory heap instead of static allocation.

## 5.3 Streaming Content Pipeline
Load standard files efficiently in the background.

**Implementation Steps:**
1. **glTF Parsing in C#:**
   - Drop a library like `SharpGLTF` into `d:\EngineSource\ArisenEngine\Engine\Arisen\3rdparty\` or as a NuGet package.
2. **Async Uploads:**
   - C# parses the glTF buffers -> Commands given to the `TransferQueue` -> Main timeline semaphore unlocks `GraphicsQueue` when rendering is safe.
