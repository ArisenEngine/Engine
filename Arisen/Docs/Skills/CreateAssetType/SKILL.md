---
name: create_asset_type
description: "How to define and serialize a new custom Asset Type."
---

# Defining a Custom Asset

Assets in Arisen Engine must be trackable via `AssetMetadata` and fast to load.

## Rules:
1. Every asset requires a stable `Guid` for referencing.
2. Source files (JSON/.meta) are for Editor use. Runtime data should be baked/cooked into binary formats to avoid string parsing or GC allocations during gameplay.
3. Keep runtime representation out of managed classes if it is large (e.g., thousands of vertices). Use unmanaged memory.

## Example: Custom Config Asset

```csharp
using System;
using ArisenEngine.Core.Assets;

namespace ArisenEngine.Engine.Assets
{
    /// <summary>
    /// A custom data-driven asset type.
    /// </summary>
    public class WeaponConfigAsset
    {
        // Stable reference to the asset
        public Guid AssetId { get; set; }

        public float BaseDamage { get; set; }
        public float FireRate { get; set; }
        
        // References to other assets MUST be via Guid, not string paths!
        public Guid FireSoundId { get; set; }
        public Guid ProjectileMeshId { get; set; }
    }
}
```
