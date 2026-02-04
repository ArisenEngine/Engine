# Arisen Vulkan RHI Modernization Roadmap

Target: Build a **Stateless**, **Handle-based**, **Highly Parallel**, and **C# Interop Friendly** high-performance rendering backend.

---

## 0. Core Design Principles

*   **Handle-Based Architecture**: All resources are referenced via 64-bit POD handles (`Index + Generation`), solving virtual table overhead and cross-language lifecycle issues.
*   **Lock-Free Multi-Threading**: Core hot paths (handle allocation, command recording) must use atomic operations and Thread-Local Storage (TLS) to ensure multi-core scalability.
*   **API Purity & Interop**: Keep interfaces blittable/flat. RHI 提供能力，不做复杂决策。
*   **Modern Features First**: Default to Synchronization 2.0, Dynamic Rendering, and Bindless. No legacy baggage.
*   **Layer Separation**: RHI 保持纯粹薄封装，Material/RenderGraph 等语义抽象交由 C# 上层实现。

---

## 1. Phase 1: Handle & Interop Polish ✅ (已完成)

**Goal**: Finalize the transition from "Object/Pointer" to "Data/Handle" ensuring complete safety across the C# boundary.

- [x] **Handle Infrastructure**
    - [x] `RHIHandle<T>` POD structure (32-bit Index + 32-bit Generation).
    - [x] Basic `RHIResourcePool` implementation (Lock-Free Atomic Stack).
- [x] **Interop Layer Cleanup**
    - [x] Remove deprecated/stub functions.
    - [x] All APIs accept `RHIHandle` types instead of `void*`.
- [x] **Thread-Local Command Management**
    - [x] TLS Command Pools with per-thread `VkCommandPool`.
    - [x] Submit-based recycling via `RHIGpuTicket` tracking.

---

## 2. Phase 2: API Cleanup & Batch Operations (当前优先)

**Goal**: 统一命名、完善错误处理、提供批量 API 支持高效 P/Invoke。

### 2.1 Export API 规范化
- [ ] **命名统一**
    - [ ] 统一使用 `RHI_Cmd_*` 前缀
    - [ ] 修复拼写错误 `AquireCurrentImage` → `AcquireCurrentImage`
    - [ ] 统一 `Create`/`Release` 配对（移除 `Destroy`）
- [ ] **生命周期约定**
    - [ ] `Create*` = Owned (需 Release)
    - [ ] `Get*/Acquire*` = Borrowed (不需 Release)
    - [ ] 添加 `@ownership` 文档注释
- [ ] **错误处理**
    - [ ] 实现 `RHI_GetLastError()` / `RHI_GetLastErrorMessage()`

### 2.2 批量操作 API
- [ ] `RHI_PSO_BatchUpdateDescriptors` - 批量 Descriptor 更新
- [ ] `RHI_Device_BatchCreateBuffers` - 批量资源创建
- [ ] `RHI_Cmd_BatchPipelineBarrier` - 批量 Barrier

### 2.3 接口简化
- [ ] 移除 `frameIndex` 冗余传递（在 `Begin` 时绑定）
- [ ] 添加 `RHI_SwapChain_BeginFrame` / `EndFrame` 简化

---

## 3. Phase 3: Performance & Stability

**Goal**: 提升性能，确保高并发稳定性。

### 3.1 Pipeline 系统
- [ ] **PSO 缓存持久化**: `VkPipelineCache` 序列化到磁盘
- [ ] **Push Constants 支持**: 减少 UBO 更新开销
- [ ] **Specialization Constants**: Shader 变体优化

### 3.2 Descriptor 优化
- [ ] **Update Template**: 使用 `vkUpdateDescriptorSetWithTemplate`
- [ ] **预分配策略**: 减少每帧重分配开销

### 3.3 命令录制
- [ ] **Secondary Command Buffer**: 细粒度多线程录制
- [ ] **Multi-Draw Indirect**: 批量绘制支持
- [ ] **Async Compute Queue**: 独立计算队列

### 3.4 基础设施健壮性
- [ ] **Thread Pool Cleanup**: 短生命周期线程的资源回收
- [ ] **Lock-Free Recycling**: 跨线程命令缓冲回收

---

## 4. Phase 4: Debug & Tooling

**Goal**: 完善调试支持和开发工具。

- [ ] **资源命名**: `RHI_SetObjectName()` 支持 RenderDoc/PIX
- [ ] **GPU 调试标记**
    - [ ] `RHI_Cmd_BeginDebugLabel` / `EndDebugLabel`
    - [ ] `RHI_Cmd_InsertDebugMarker`
- [ ] **Barrier 辅助工具**
    - [ ] `TransitionImageLayout` 简化常见 Transition
    - [ ] `RHI_VALIDATION` 模式下的资源状态验证
- [ ] **RHI Inspector**: 查看 Handle 计数和内存使用

---

## 5. Phase 5: Advanced Features (长期计划)

**Goal**: 现代 GPU 高级特性支持。

### 5.1 已完成
- [x] Tessellation Shaders
- [x] Geometry Shaders
- [x] Mesh Shaders (`VK_EXT_mesh_shader`)

### 5.2 待实现
- [ ] **Ray Tracing**
    - [ ] 加速结构 (BLAS/TLAS) 构建
    - [ ] RT Pipeline 支持
    - [ ] Ray Query (Inline Ray Tracing)
- [ ] **Variable Rate Shading**: `VK_KHR_fragment_shading_rate`
- [ ] **GPU Timeline Semaphores**: 更精细的同步控制
- [ ] **Transient Resource Aliasing**: VMA 内存别名优化

---

*Last Updated: 2026-02-04*

## Architecture Decision Records

| 决策 | 结论 | 理由 |
|------|------|------|
| Material 抽象 | C# 上层 | RHI 保持纯粹，语义抽象交给上层 |
| 自动 Barrier | C# RenderGraph | 需要全局依赖视图，RHI 只提供辅助工具 |
| 批量 API | RHI 层 | 减少 P/Invoke 开销 |
| 资源生命周期 | 命名约定 | `Create*` = Owned, `Get*` = Borrowed |
