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
- [x] **API Consistency**
    - [x] Verify `AllocBuffer` vs `AllocBufferDeviceMemory` usage pattern is thread-safe or properly synchronized at the factory level.

---

## 2. Phase 2: High-Performance Infrastructure (The Next Big Step)

**Goal**: Eliminate global mutexes in hot paths. Enable true multi-threaded recording.

- [x] **Lock-Free Resource Pools**
    - [x] **Atomic Registry**: Replace `std::mutex` in `RHIResourcePool` with `std::atomic` free-lists and generation counters.
    - [x] **Memory Strategy**: Use chunked/paged memory for pools to avoid `std::vector` resize locks and pointer invalidation.
- [x] **Thread-Local Command Management**
    - [x] **TLS Command Pools**: Implement `RHIVkCommandBufferPool` that caches `VkCommandPool` per thread.
    - [x] **Submit-based Recycling**: Ensure Command Buffers are only recycled after their specific submission is complete (via `RHIGpuTicket` tracking). 

---

## 3. Phase 3: Advanced Rendering Features & Robustness

**Goal**: Leverage modern GPU features and ensure system stability under high-concurrency loads.

- [ ] **Infrastructure Robustness**
    - [ ] **Thread Leak Prevention**: Implement cleanup logic for `m_ThreadPools` in `RHIVkCommandBufferPool` to prevent memory growth when threads are short-lived.
    - [ ] **Lock-Free Recycling**: Transition from mutex-guarded Maps to lock-free structures (e.g., `MPSCQueue`) for cross-thread command buffer recycling.
    - [ ] **Capture Optimization**: Improve `CaptureResource` with faster deduplication (e.g., BitSet or Tagging) for complex command buffers.
- [ ] **Pipeline & Shader System**
    - [ ] **SPIRV-Reflect Integration**: Automatically extract descriptor layouts from shader bytecode. Remove manual `PipelineLayout` creation requirements.
    - [ ] **PSO Caching**: Implement `VkPipelineCache` serialization to disk to reduce startup hitching.
- [ ] **Memory Optimization**
    - [ ] **Transient Resource Aliasing**: Implement memory reuse for non-overlapping resources (e.g., FrameGraph attachments) using VMA aliasing.

---

## 4. Phase 4: Validation & Tooling

- [ ] **Debug Tooling**: Implement an "RHI Inspector" to view active handle counts and memory usage per pool.
- [ ] **Handle Validation**: Add `RHI_VALIDATION` macro to enable/disable generation checking overhead in Release builds.

---
*Last Updated: 2026-01-27*

🚀 第一阶段：基础设施自动化与并行验证 (最高优先级)
在引入新 Shader Stage 之前，先要把现有的“地基”打稳，让后续开发更丝滑。

[Task] SPIRV-Reflect 自动化闭环
目前 PSO 已能自动提取布局，但需补全对更多 Resource Type（如 Storage Image, Input Attachment）及新阶段（Geometry/Tess/Mesh）的支持。
目标：彻底摆脱手动在 C++ 里写 AddDescriptorSetLayoutBinding。
[Task] 多线程录制验证 (Multi-threaded Recording Test)
编写一个专门的测试用例，模拟 4-8 个线程并行录制不同的物体指令，验证 TLS Command Pool 的正确性。
🎨 第二阶段：现代管线推进 (高优先级)
Mesh Shader 是未来的趋势，也是你最关心的现代特性。

[Task] Mesh Shader 核心支持
在 RHIVkInstance 中开启 VK_EXT_mesh_shader 特性。
更新 PSO 以兼容 Task Shader 和 Mesh Shader 阶段。
学习点：理解如何跳过 Vertex Buffer，直接在 Mesh Shader 里生成几何数据。
📚 第三阶段：传统管线补完 (中优先级 - 学习导向)
这一阶段主要满足你对经典管线的好奇。

[Task] Geometry Shader (几何着色器)
在 Device 开启 geometryShader 特性。
更新 RHI 的 Reflection 和 Stage 映射。
学习点：尝试实现一个简单的法线可视化或粒子展开（Billboard）。
[Task] Tessellation Shaders (曲面细分)
开启 tessellationShader 特性。
在 PSO 中添加对 VkPipelineTessellationStateCreateInfo（Patch Control Points）的支持。
学习点：实现一个简单的地形细分（LOD）或平滑。
💎 第四阶段：图形学“圣杯” (长期计划)
[Task] Ray Tracing (光线追踪)
开启 RT 模型相关的 10+ 个扩展。
实现加速结构 (AS) 构建、RT Pipeline 等。
