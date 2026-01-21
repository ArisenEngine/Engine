# Vulkan RHI Modernization Roadmap

目标：构建一个现代、高性能、低开销且易于扩展的 Vulkan 渲染底层封装。

## 优先级 1：核心基础设施优化 (Stability & Foundation)

- [x] **集成 VMA (Vulkan Memory Allocator)**
  - [x] 替换当前的 `RHIVkDeviceMemory` 手动分配逻辑。
  - [x] 实现高效的内存分池、对齐和整理，减少分配开销。
- [x] **升级至 Synchronization 2.0 (`VK_KHR_synchronization2`)**
  - [x] 使用更清晰的 `VkDependencyInfo` 替换旧的 Pipeline Barrier。
  - [x] 简化 Resource Barrier 的接口封装，支持全局同步状态管理。
- [x] **支持 Dynamic Rendering (`VK_KHR_dynamic_rendering`)**
  - [x] 在核心流程中逐步替代传统的 `VkRenderPass` 和 `VkFramebuffer`。
  - [x] 减少 RHI 层的对象管理复杂度，提高灵活性。

## 优先级 2：性能与易用性提升 (Performance & Usability)

- [x] **Bindless Resource 架构实现**
  - [x] 实现基于全局 Descriptor Set 的资源绑定（Descriptor Indexing）。
  - [x] 支持 `update-after-bind`，减少 Descriptor 更新导致的 CPU 等待。
- [ ] **Pipeline State Object (PSO) 缓存管理**
  - 实现 `VkPipelineCache` 的持久化存储（序列化到磁盘）。
  - 优化 PSO 创建流程，利用多线程预初始化常用 Pipeline。
- [ ] **多线程 Command Buffer 录制优化**
  - 优化 `RHIVkCommandBufferPool` 的线程分配策略。
  - 支持 Secondary Command Buffers 并行录制（针对高 Draw Call 场景）。
- [ ] **统一的 Descriptor 管理策略**
  - 引入更智能的 Descriptor Pool 自动扩容与回收机制。
  - 针对 Dynamic Uniform Buffers 提供更低开销的分配路径。

## 优先级 3：进阶功能与架构扩展 (Advanced Features)

- [ ] **Transient Resource & Aliasing (瞬时资源与重用)**
  - 实现 RenderTarget 的内存复用（Aliasing），降低显存占用。
  - 为 FrameGraph 的集成打下基础。
- [ ] **GPU-Driven Rendering 基础设施**
  - 完善 `DrawIndirect` 和 `DrawIndexedIndirect` 支持。
  - 为 GPU 端的 Culling 和 LOD 切换提供接口。
- [ ] **Shader 反射与自动 Layout 生成**
  - 集成 SPIRV-Reflect，自动从 Shader 中提取 Descriptor Layout 和 Push Constant 定义。
  - 减少手动声明 `RHIVkDescriptorSetLayout` 的繁琐过程。
- [ ] **Mesh Shader & Ray Tracing 支持 (可选)**
  - 为次世代特性预留扩展接口。

---
*注：此文档将根据开发进度动态更新。优先完成优先级 1 的内容以确保底层稳定性。*

## 优先级 1.5：现代 API 架构与 C# 互操作准备 (Modern API & Interop) -> *[NEW/CRITICAL]*
> 为了解决 "C# Binding" 和当前调用方式 "怪怪的" (Mixed OO/C-Style) 问题，建议重构底层为面向数据的 Handle-based 架构。

- [ ] **RHI 句柄化重构 (Handle-Based Architecture)**
  - [ ] **Phase 1: 基础类型与资源池 (Foundation)**
    - [ ] Define `RHIHandle` POD structs (Index + Generation) for all resources.
    - [ ] Implement `RHIResourcePool` for lifecycle management & handle lookups.
    - [ ] Ensure all Handles are Blittable for C# interop.
  - [ ] **Phase 2: 渐进式迁移 (Incremental Migration)**
    - [ ] *Resources*: Add handle-returning `Create` functions alongside existing ones.
    - [ ] *Descriptors*: Update `UpdateDescriptorSets` to accept handles.
    - [ ] *Pipelines*: Refactor Pipeline creation to return handles.
    - [ ] *Commands*: Abstract CommandBuffer to use handles for barriers/draws.
  - [ ] **Phase 3: 清理与强制转型 (Cleanup)**
    - [ ] Mark pointer-based interfaces `[[deprecated]]`.
    - [ ] Remove `virtual` interface pointer passing.
    - [ ] Finalize stateless API design.
- [ ] **API 清理与命名规范化 (API Cleanup)** -> *[NEW]*
  - [ ] 移除过时/废弃接口 (如 `GetHandle` vs `GetHandlerPointer` 的歧义)。
  - [ ] 统一命名规范 (e.g., `Cmd` 前缀用于 CommandBuffer 命令, `Alloc/Free` vs `Create/Destroy` 语义明确化)。
  - [ ] 确保对外暴露的 C++ 接口风格统一，便于自动 Binding 生成。

## 优先级 2：性能与易用性提升 (Performance & Usability)

- [x] **Bindless Resource 架构实现**
  - [x] 实现基于全局 Descriptor Set 的资源绑定（Descriptor Indexing）。
  - [x] 支持 `update-after-bind`，减少 Descriptor 更新导致的 CPU 等待。
- [ ] **加强 Queue 管理 (Queue Management)** -> *[NEW]*
  - [ ] 显式暴露 `RHIQueue` 概念 (Graphics, Compute, Transfer)。
  - [ ] 实现 `Async Compute` 队列的探测与提交逻辑。
  - [ ] 支持 Queue 之间的 Ownership Transfer Barrier (针对跨队列资源同步)。
- [ ] **Compute Shader 完整支持** -> *[NEW]*
  - [ ] 新增 `Dispatch` 和 `DispatchIndirect` 命令接口。
  - [ ] 抽象 `ComputePipeline`，分离于当前的 `GPUPipeline` (Graphics-focused)。
- [ ] **Pipeline State Object (PSO) 缓存管理**
  - 实现 `VkPipelineCache` 的持久化存储（序列化到磁盘）。
  - 优化 PSO 创建流程，利用多线程预初始化常用 Pipeline。
- [ ] **多线程 Command Buffer 录制优化**
  - 优化 `RHIVkCommandBufferPool` 的线程分配策略。
  - 支持 Secondary Command Buffers 并行录制（针对高 Draw Call 场景）。
- [ ] **统一的 Descriptor 管理策略**
  - 引入更智能的 Descriptor Pool 自动扩容与回收机制。
  - 针对 Dynamic Uniform Buffers 提供更低开销的分配路径。

## 优先级 3：进阶功能与架构扩展 (Advanced Features)

- [ ] **GPU-Driven Rendering 基础设施**
  - 完善 `DrawIndirect` 和 `DrawIndexedIndirect` 支持。
  - [ ] **Multi-Draw 支持**: 实现 `vkCmdDrawIndexedIndirectCount` 等高效批量绘制。 -> *[NEW]*
  - [ ] **Mesh Shader 支持**: 引入 `DrawMeshTasks`。 -> *[NEW]*
- [ ] **Transient Resource & Aliasing (瞬时资源与重用)**
  - 实现 RenderTarget 的内存复用（Aliasing），降低显存占用。
  - 为 FrameGraph 的集成打下基础。
- [ ] **Shader 反射与自动 Layout 生成**
  - 集成 SPIRV-Reflect，自动从 Shader 中提取 Descriptor Layout 和 Push Constant 定义。
  - 减少手动声明 `RHIVkDescriptorSetLayout` 的繁琐过程。
- [ ] **Ray Tracing 支持 (可选)**
  - 为次世代特性预留扩展接口。