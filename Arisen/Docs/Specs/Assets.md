# Asset Pipeline Spec

The Arisen Engine Asset Pipeline must be highly performant, data-driven, and suitable for the next generation of games. It must minimize Load-Time overhead and GC pressure.

## 1. The Metadata File (`.meta`)
Every source asset (e.g., `.png`, `.fbx`, `.cs`) must have a corresponding `.meta` file.
- **Structure**: Uses `AssetMetadata` containing a stable `Guid`. 
- **Serialization**: `.meta` files are typically serialized via JSON/YAML for readability and version control diffing.
- **Reference Resolution**: Systems **never** reference assets by their string path (e.g., `"Assets/Tex.png"`). They reference them by `Guid` to prevent breakages when files are moved.

## 2. Fast Loading & Unloading
- **Binary Cooked Data**: While source assets are human-readable or complex formats, the Engine does **not** parse them at runtime. A "Cook" or "Import" step processes them into highly optimized, memory-mappable Binary blocks.
- **Zero-Copy Loading**: The ideal asset load (e.g., a Mesh) is memory-mapped directly from disk into an unmanaged memory buffer, then uploaded to the GPU via RHI, bypassing C# Garbage Collection entirely.
- **Streaming**: Large assets (Audio, Textures, sprawling Scenes) must support asynchronous streaming.

## 3. Data-Driven ECS Prefabs
- "Prefabs" or Scene data in Arisen are simply serialized lists of Entity IDs and their corresponding `IComponent` data.
- Deserializing a Scene means allocating Entities in bulk and memcpy-ing the component data directly into the `ComponentPool<T>` dense arrays.
