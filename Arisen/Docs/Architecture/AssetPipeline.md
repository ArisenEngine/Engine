# Architecture Spec: Asset Pipeline & Resource Management

**Status**: Draft / Active  
**Module**: `ArisenKernel` (`IAssetDatabase`, `ResourceRegistry`)

In a Data-Oriented, Zero-Overhead engine, the way assets (Textures, Meshes, Audio, Scenes) are loaded from disk into memory is paramount. String parsing, JSON deserialization, and large heap allocations during runtime loading will immediately destroy target frame rates ("hitches").

Arisen Engine completely solves this using a strict **Cooked Binary Asset Pipeline**.

---

## 1. Golden Rule: No String Paths at Runtime
**NEVER** load or reference an asset by its filesystem path at runtime.
```csharp
// FATAL ERROR IN CODE REVIEW:
var texture = AssetDatabase.Load<Texture>("Assets/Models/Player/Skin.png");
```

Files are renamed, moved to new folders, and renamed again. Hardcoded string paths guarantee broken game builds. 

In Arisen, **all assets are referenced via a `Guid`**.
```csharp
// CORRECT:
var texture = AssetDatabase.Load<Texture>(new Guid("A1B2C3D4-..."));
```

---

## 2. Source Assets vs. Meta Files

When a designer drags a raw asset (e.g., `Sword.fbx` or `Skin.png`) into the `Assets/` folder, the Editor's Asset Importer immediately generates an adjacent `.meta` file written in YAML.

### The Meta File (`Skin.png.meta`)
```yaml
guid: a1b2c3d4-e5f6-7890-1234-56789abcdef0
type: Texture2D
importerSettings:
  compression: DXT5
  generateMipMaps: true
```

1. **The Guid is Stable**: The Guid is generated once and forever belongs to that asset. If the user moves `Skin.png` to `Assets/Weapons/Skin.png`, the Editor simply moves the `.meta` file alongside it. Any Component wrapping that Guid structurally remains perfectly intact.
2. **YAML**: As defined in `ConfigurationFormats.md`, we use YAML here because human developers frequently need to merge `.meta` files in Git, and YAML handles line-by-line merges cleanly.

---

## 3. The Cooking Pipeline (Zero-Copy Loading)

Arisen Engine **does not** load raw `.png` or `.fbx` files at runtime. Parsing PNG headers or JSON models during the game loop is overwhelmingly slow.

Instead, we use a **Cooking Pipeline**:
1. During the Build process (or Editor import), the engine reads the raw source asset and the `.meta` file's settings.
2. It compiles ("cooks") the data into an optimized, memory-aligned **Binary Blob** (`.arisenasset`).
3. For example, a `.png` is decompressed, converted to raw GPU-ready DXT5 bytes, and saved directly to the `.Cache/CookedAssets/{Guid}.arisenasset` folder.

### Zero-Copy Loading
When the engine runs and a subsystem calls `AssetDatabase.Load(guid)`, the Engine does NOT parse data. 
It memory-maps (`mmap`) the cooked binary blob directly from the SSD into RAM. Because the binary blob was structurally laid out during the Cooking phase to exactly match the C# `struct` or C++ layout, the CPU does zero processing. The bytes are instantly recognizable.

---

## 4. The Asset Database & Lifecycle

The `IAssetDatabase` (provided by a core Service) acts as the global librarian.

1. **Manifest File**: During the build, the Asset Pipeline generates a global `AssetManifest.json` (or binary equivalent) that contains a giant lookup table mapping `Guid` -> `CookedBinaryPath`.
2. **Runtime Loading**: When a Guid is requested, the Database checks its `Dictionary<Guid, IntPtr>` cache. If it isn't loaded, it finds the binary path from the manifest, mapped the file, and hands back the memory pointer.
3. **Reference Counting**: The Database tracks how many systems are currently holding a handle to an asset. When the reference count drops to 0, the AssetDatabase explicitly unloads the memory pointer to prevent RAM bloat.
