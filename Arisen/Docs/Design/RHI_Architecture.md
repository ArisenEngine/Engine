# Arisen RHI Architecture & Design

The Render Hardware Interface (RHI) is the foundational translation layer between ArisenEngine's logic and the physical GPU. Currently mapped predominantly to **Vulkan** (with DX12 provisions), the RHI is built in modern C++ to guarantee highest-throughput and lowest possible latencies.

## 1. The Design Philosophy

The RHI is deliberately kept feature-lean, avoiding heavy state tracking or arbitrary memory allocations. 

- **Data First:** Objects like buffers, images, and pipelines are essentially opaque handles (`uint32_t` or equivalent pointers).
- **No Handholding:** The Engine (`C#` logic) is responsible for organizing resource transitions (Pipeline Barriers), memory alignments, and Pipeline State Object (PSO) caching. The C++ RHI does entirely what it is told to do.

## 2. Capability Systems & Extensions

A major pillar for rendering robustness is the `RHICapabilities` interface. The RHI will dynamically probe hardware capabilities during device creation rather than forcing hardcoded extensions.

### Handled Capabilities
- **Dynamic Rendering (`VK_KHR_dynamic_rendering`)**: To eliminate antiquated `VkRenderPass` and `VkFramebuffer` constraints.
- **Descriptor Buffers (`VK_EXT_descriptor_buffer`)**: To bypass the CPU overhead of Descriptor Sets and pools.
- **Shader Objects (`VK_EXT_shader_object`)**: To prevent pipeline compilation stutter by shedding rigid Pipeline State Objects where supported.

If a GPU lacks specific top-tier capabilities (e.g. `dynamic_rendering`), the capability system must notify the Engine to utilize fallback paths if supported.

## 3. Multi-Queue Topologies

Modern GPU performance requires breaking explicit synchronization to single-queues. Moving forward, the `Submit` mechanism leverages three distinct streams:

1. **`TransferQueue`**: Highly-efficient DMA copy streams. Used asynchronously by the AssetPipeline to upload massive textures (`glTF`, `KTX2`) into GPU local memory without halting graphics.
2. **`ComputeQueue`**: Asynchronous dispatch operations. Can run culling grids, particle physics, or skinning loops concurrently while the main Graphics pipeline is shadowing or shading.
3. **`GraphicsQueue`**: Dedicated to rasterization. Requires strict `RHISyncPrimitive` (`Semaphore` and `Fence`) guarantees to wait for the proper Transfer and Compute queues to yield logic before drawing.

## 4. Multi-Threaded Command Recording

To match the C# JobSystem, command recording must be purely lock-free. 
- **Command Streams**: RHI CommandBuffers use a linear memory arena internally (`vector/array bytes`). Resizing during a frame triggers massive GC/Memory jitter. The arena guarantees zero-allocation `memcpy` commands.
- **Thread Local Registries**: The handle registry removes the global mutex in favor of a thread-safe dictionary/array lookup, guaranteeing non-blocking C# recording.

## 5. C# Mapping (`AutoBinding`)

Functions exported from `Core.RHI` are generated automatically via `CppSharp` in the `BindingGenerator`. This abstracts C++ structs directly over into `ref` / `in` pointers inside `ArisenEngine`'s C# boundary, bypassing standard .NET memory marshes and invoking natively.
