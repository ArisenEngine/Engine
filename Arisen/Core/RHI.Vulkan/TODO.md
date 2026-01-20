# Vulkan RHI Modernization Roadmap

目标：构建一个现代、高性能、低开销且易于扩展的 Vulkan 渲染底层封装。

## 优先级 1：核心基础设施优化 (Stability & Foundation)

- [x] **集成 VMA (Vulkan Memory Allocator)**
  - [x] 替换当前的 `RHIVkDeviceMemory` 手动分配逻辑。
  - [x] 实现高效的内存分池、对齐和整理，减少分配开销。
- [x] **升级至 Synchronization 2.0 (`VK_KHR_synchronization2`)**
  - [x] 使用更清晰的 `VkDependencyInfo` 替换旧的 Pipeline Barrier。
  - [x] 简化 Resource Barrier 的接口封装，支持全局同步状态管理。
- [ ] **支持 Dynamic Rendering (`VK_KHR_dynamic_rendering`)**
  - 在核心流程中逐步替代传统的 `VkRenderPass` 和 `VkFramebuffer`。
  - 减少 RHI 层的对象管理复杂度，提高灵活性。

## 优先级 2：性能与易用性提升 (Performance & Usability)

- [ ] **Bindless Resource 架构实现**
  - 实现基于全局 Descriptor Set 的资源绑定（Descriptor Indexing）。
  - 支持 `update-after-bind`，减少 Descriptor 更新导致的 CPU 等待。
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