# Arisen Vulkan RHI 现代化升级路线图 (Modernization Roadmap)

目标：构建一个**无状态 (Stateless)**、**句柄化 (Handle-based)**、**高并行 (Highly Parallel)** 且对 **C# 自动绑定友好** 的高性能渲染底层。

---

## 0. 核心设计原则 (Design Principles)

*   **Handle-Based Architecture**: 所有资源引用通过 64 位 POD 句柄 (`Index + Generation`) 进行，彻底解决虚表开销与跨语言生命周期管理痛点。
*   **Lock-Free Multi-Threading**: 核心路径（申请句柄、命令录制、引用计数）通过原子操作和 Thread-Local 存储实现无锁化，确保多核缩放性。
*   **API Purity & Interop**: 保持接口 Blittable，移除 C++ 虚接口在核心录制路径的硬依赖，支持自动 P/Invoke 绑定生成。
*   **Modern Features First**: 默认启用 Synchronization 2.0、Dynamic Rendering 和 Bindless，不背负旧版 Vulkan 的历史包容。

---

## 1. 第一阶段：句柄化重构与 C# 互操作 (Current Focus: Handle-Based & Interop)

### 迭代目标
完成从“对象/指针”模式向“数据/句柄”模式的彻底转型，确保底层资源管理与宿主语言（C#）解耦。

- [x] **句柄基础架构实现**
    - [x] 定义 `RHIHandle` POD 结构（32-bit Index + 32-bit Generation）。
    - [ ] **完善 RHIResourcePool/Registry**
        - [ ] 针对 Buffer, Image, Sampler 等轻量级资源实现特化池。
        - [ ] 实现基础的多线程安全分配（当前已使用 Mutex，下阶段升级）。
- [ ] **接口层全面句柄化 (The Great Refactoring)**
    - [ ] **Device 接口**: `CreateBuffer` 等接口返回 `RHIBufferHandle` 而非 `unique_ptr`。
    - [ ] **Command 接口**: 所有 `CmdXXX` 命令参数从指针切换为 Handle，移除 `virtual` 调用。
    - [ ] **Descriptor 升级**: `UpdateDescriptorSets` 接受 Handle 数组，简化 C# 端的内存封送。
- [ ] **C# 绑定基础设施**
    - [ ] 实现 `RHIExports.cpp`，暴露 C-style 扁平化接口给 C# 调用。

> **设计抉择提示 (Rationale)**: 
> 为什么坚持 Handle-based？因为 C# 垃圾回收器无法感知指针背后的 GPU 资源生命周期。通过 Handle，我们可以在 C++ 端统一维护索引池和引用计数，C# 端只需持有一个 64 位整数，安全性与性能兼得。

---

## 2. 第二阶段：无锁化多线程基础设施 (Lock-Free Infrastructure)

### 迭代目标
消除核心路径中的全局互斥锁，实现高并发录制。

- [ ] **无锁资源管理 (Lock-Free Registry)**
    - [ ] **方案实现**: 使用 `std::atomic<UInt32>` 替换 `RHIResourceRegistry` 中的 `refCount`。
    - [ ] **方案实现**: 使用原子操作（如 `compare_exchange`）管理 `FreeList` 索引，实现 `Allocate/Release` 的无锁化。
    - [ ] **方案实现**: 资源池预分配大容量地址空间，利用内存分页避免 `std::vector` 扩容导致的全局挂起。
- [ ] **Thread-Local 命令池优化**
    - [ ] 升级 `RHIVkCommandBufferPool`，利用 `thread_local` 缓存每个线程的 `VkCommandPool`。
    - [ ] 实现基于帧序号 (FrameIndex) 的多级回收机制，确保 Command Buffer 在提交完成前不被重置。
- [ ] **异步提交与负载均衡 (Async Submission)**
    - [ ] 抽象 `RHICommandQueueManager`，负责跨线程的任务聚合与 `Async Compute` 优先级调度。

> **设计抉择提示 (Rationale)**: 
> Vulkan 的精髓在于并行性。如果 `Allocate` 资源或 `CmdDraw` 时还在争抢同一个全局 `std::mutex`，现代 CPU 的多核优势将被白白浪费。

---

## 3. 第三阶段：现代特性与基建完善 (Next-Gen Infrastructure)

### 迭代目标
结合现代图形特性，简化 PSO 管理，降低 CPU 提交开销。

- [ ] **自动化 Pipeline 管理 (Infrastructure)**
    - [ ] **SPIRV-Reflect 集成**: 自动从 Shader 字节码提取 Layout 信息，移除手动声明 `VkDescriptorSetLayout` 的繁琐过程。
    - [ ] **持久化 PSO 缓存**: 实现 `VkPipelineCache` 的磁盘序列化，解决“初次运行卡顿”问题。
- [ ] **瞬时资源与内存复用 (Transient Assets)**
    - [ ] 基于 VMA 实现内存别名 (Aliasing)，支持 RenderTarget 在 RenderPass 间的显存复用（针对 FrameGraph 深度优化）。
- [ ] **GPU-Driven 基础设施**
    - [ ] 完善 `Multi-Draw Indirect (MDI)` 封装，支持 GPU 端剔除后的批量绘制。
    - [ ] 统一的 Bindless Descriptor 管理器，支持按需动态更新全局索引表。

> **设计抉择提示 (Rationale)**: 
> 手动维护 Pipeline Layout 是 Vulkan 开发中最容易出错的地方。通过反射机制结合 Handle 管理，可以极大降低上层逻辑（如 Material System）的使用难度。

---

## 4. 第四阶段：稳定性检测与生产工具 (Validation & Tooling)

- [ ] **RHI 自省工具**: 实现资源视图，实时查看各种 Pool 的内存分布、Handle 存活情况。
- [ ] **增强 Validation 注入**: 在 Debug 模式下将 RHI 层面的状态错误映射到具体的 VUID 解释。
- [ ] **多层级性能标记 (Tuning)**: 实现核心流程的埋点，支持导出到 NSight / RenderDoc 进行深度分析。

---
*上次更新日期: 2026-01-21*
*迭代负责人: Antigravity*
