# Arisen Vulkan RHI Modernization Roadmap

Target: Build a **Stateless**, **Handle-based**, **Highly Parallel**, and **C# Interop Friendly** high-performance rendering backend.

---

## 0. Core Design Principles

*   **Handle-Based Architecture**: All resources are referenced via 64-bit POD handles (`Index + Generation`), solving virtual table overhead and cross-language lifecycle issues.
*   **Lock-Free Multi-Threading**: Core hot paths (handle allocation, command recording) must use atomic operations and Thread-Local Storage (TLS) to ensure multi-core scalability.
*   **API Purity & Interop**: Keep interfaces blittable/flat. Remove C++ virtual interfaces from hot recording paths to support automatic P/Invoke binding.
*   **Modern Features First**: Default to Synchronization 2.0, Dynamic Rendering, and Bindless. No legacy baggage.

---

## 1. Phase 1: Handle & Interop Polish (Current Status: ~90%)

**Goal**: Finalize the transition from "Object/Pointer" to "Data/Handle" ensuring complete safety across the C# boundary.

- [x] **Handle Infrastructure**
    - [x] `RHIHandle<T>` POD structure (32-bit Index + 32-bit Generation).
    - [x] Basic `RHIResourcePool` implementation (currently Mutex-guarded).
- [x] **Interop Layer Cleanup**
    - [x] **`HandlesExports.cpp`**: Remove deprecated/stub functions (`RHI_Buffer_Alloc`, `RHI_Image_Alloc`) to prevent misuse.
    - [x] **`DeviceExports.cpp`**: Ensure all APIs accept `RHIHandle` types instead of `void*` or raw pointers for strict type safety.
- [ ] **API Consistency**
    - [ ] Verify `AllocBuffer` vs `AllocBufferDeviceMemory` usage pattern is thread-safe or properly synchronized at the factory level.

---

## 2. Phase 2: High-Performance Infrastructure (The Next Big Step)

**Goal**: Eliminate global mutexes in hot paths. Enable true multi-threaded recording.

- [ ] **Lock-Free Resource Pools**
    - [ ] **Atomic Registry**: Replace `std::mutex` in `RHIResourcePool` with `std::atomic` free-lists and generation counters.
    - [ ] **Memory Strategy**: Use chunked/paged memory for pools to avoid `std::vector` resize locks and pointer invalidation.
- [ ] **Thread-Local Command Management**
    - [ ] **TLS Command Pools**: Implement `RHIVkCommandBufferPool` that caches `VkCommandPool` per thread.
    - [ ] **Frame-Local Recycling**: Ensure Command Buffers are only recycled after the GPU has finished the frame (FrameIndex tracking).

---

## 3. Phase 3: Advanced Rendering Features

**Goal**: Leverage modern GPU features to simplify upper-layer logic and improve performance.

- [ ] **Pipeline & Shader System**
    - [ ] **SPIRV-Reflect Integration**: Automatically extract descriptor layouts from shader bytecode. Remove manual `PipelineLayout` creation requirements.
    - [ ] **PSO Caching**: Implement `VkPipelineCache` serialization to disk to reduce startup hitching.
- [ ] **Memory Optimization**
    - [ ] **Transient Resource Aliasing**: Implement memory reuse for non-overlapping resources (e.g., FrameGraph attachments) using VMA aliasing.
    - [ ] **Async Transfer Queue**: Offload heavy resource uploads (`vkCmdCopyBufferToImage`) to a dedicated Transfer queue/thread.

---

## 4. Phase 4: Validation & Tooling

- [ ] **Debug Tooling**: Implement an "RHI Inspector" to view active handle counts and memory usage per pool.
- [ ] **Handle Validation**: Add `RHI_VALIDATION` macro to enable/disable generation checking overhead in Release builds.

---
*Last Updated: 2026-01-23*
