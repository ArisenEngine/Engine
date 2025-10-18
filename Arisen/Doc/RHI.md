## RHI 现状评审与重构路线

本文记录当前 `RHI/` 与 `RHI.Vulkan/` 的问题清单、目标架构与分期里程碑，用于跟踪推进与验收。

### 当前主要问题与风险
- 抽象层泄漏/不稳定
  - Vulkan 常量/概念外泄到抽象接口（如 `VK_SUBPASS_EXTERNAL`）。
  - 标量参数被错误地声明为 `T&&`，按值返回类型不应带 `const`。
  - 重复/分散的公共结构定义（如 `GPUProgramDesc` 在多处）。
- 工厂能力过窄
  - `RHIFactory` 仅创建 `Sampler`，难以扩展到 Buffer/Image/Pipeline/Descriptor 等。
- 设备/表面/交换链耦合与生命周期不完整
  - `DestroySurface` 未实现，多窗口/多交换链策略不清晰；设备归属与窗口映射耦合。
- 能力查询未落地
  - 已有 Swapchain 能力查询，但 `GetSuitableSwapChainFormat/PresentMode` 返回固定值；`SetCurrentPresentMode` 空实现。
- 命名与一致性
  - `GraphsicsAPI` 拼写错误；同名结构在 Vulkan 与 RHI 层重复定义，存在 ODR 风险。
- 关键能力缺失
  - 队列/提交模型（Graphics/Compute/Transfer）、TimelineFence/同步抽象。
  - 资源抽象（Buffer/Image/Memory 分配器）、Barrier/状态转换、队列所有权转移。
  - 描述符（Set/Heap/Bindless）与 Pipeline/Shader 抽象（含反射与缓存）。
  - 多帧在飞管理与资源回收（见 `Architecture.md` 的回收策略）。
- 错误处理与所有权
  - 工厂返回裸指针，接口析构/销毁次序存在潜在泄漏风险；`DestroySurface`/Device 销毁顺序需保证。

### 目标架构（高层）
- Instance / Device / Surface / Swapchain 解耦
  - `RHIInstance`：实例与物理设备枚举；
  - `RHIDevice`：逻辑设备与队列/提交；
  - `RHISwapchain`：按窗口管理，支持创建/重建/销毁与模式切换。
- 队列与同步抽象
  - `RHIQueueType { Graphics, Compute, Transfer }`；
  - `RHISubmitInfo { CommandBuffers, Wait(Timeline,value), Signal(Timeline,value) }`；
  - `RHITimelineFence { signal, wait, completed }`。
- 资源/Barrier
  - `RHIBuffer/RHIImage` + 统一分配（Vulkan: VMA；D3D12: D3D12MA）；
  - `RHIBarrier { resource, oldState, newState, queueOwnership }`。
- 描述符/绑定
  - `RHIShaderModule`, `RHIDescriptorSetLayout`, `RHIDescriptorSet/Heap`, `RHIPipelineLayout`；
  - Bindless（descriptor indexing）作为 feature gated 选项。
- Pipeline/Shader
  - 图形/计算 Pipeline；Shader 反射集成（spirv-cross/dxcompiler reflection）。
- 多帧在飞与资源回收
  - 帧围栏、按提交粒度的回收（Epoch Ring + Overdue 桶，参见 `Architecture.md`）。

### 里程碑与验收标准

#### M0 API 清洁与落地（~1–2 天）
- 任务
  - 修正命名与签名：`GraphicsAPI` 拼写；移除标量 `T&&`；去掉按值返回的 `const`；`GetEnvString()` 改为 `std::wstring GetEnvString() const`。
  - 实现 `DestroySurface`、`SetCurrentPresentMode`；用真实能力选择 `GetSuitableSwapChainFormat/PresentMode`。
  - 去重 `GPUProgramDesc`：仅保留 `RHICommon.h`，Vulkan 层引用之。
  - 移除接口里直接暴露的 Vulkan 常量（如 `GetExternalIndex()` 以抽象常量或内部转换实现）。
- 验收
  - 编译通过；多窗口创建/销毁 `Surface` 正常；格式/显示模式选择反映查询结果；接口层不再出现 Vulkan 常量。

#### M1 设备/交换链职责梳理（~2–4 天）
- 任务
  - 设备与交换链解耦：`RHIDevice` 负责队列与提交，`RHISwapchain` 负责窗口相关。
  - 允许一个 `Device` 持有多 `Swapchain`；明确销毁顺序与资源回收。
- 验收
  - 支持多窗口渲染；窗口销毁/重建不影响其它窗口；无泄漏。

#### M2 队列与 TimelineFence 抽象（~3–5 天）
- 任务
  - 定义 `RHIQueue/RHISubmitInfo/RHITimelineFence`；Vulkan 用 timeline semaphore 映射，支持 Graphics/Compute/Transfer。
  - 提交/等待接口完善，提供 `completed()` 查询。
- 验收
  - Demo：向 Graphics/Transfer 队列提交并用 timeline fence 精确等待；完成值递增正确。

#### M3 资源与分配器（~4–7 天）
- 任务
  - `RHIBuffer/RHIImage` 创建参数覆盖用途/heap/初始状态；接入 VMA/D3D12MA。
  - Barrier/状态转换与队列所有权转移描述；上传路径（staging→transfer→graphics）。
- 验收
  - Demo：创建纹理/缓冲并上传，渲染使用，状态转换正确，内存稳定。

#### M4 描述符/绑定与 Bindless（~3–6 天）
- 任务
  - `RHIDescriptorSetLayout/DescriptorSet/Heap` 与 `RHIPipelineLayout`；更新/绑定接口；可选 bindless（feature 检测）。
- 验收
  - Demo：采样器/纹理绑定渲染；（可选）bindless 索引访问通过。

#### M5 Pipeline/Shader（~3–6 天）
- 任务
  - `RHIShaderModule` + 反射（布局/推常量/绑定槽）；Graphics/Compute Pipeline 抽象；缓存策略。
- 验收
  - Demo：三角形/计算着色器样例在新抽象上跑通。

#### M6 提交模型与多帧在飞整合（~2–4 天）
- 任务
  - 帧围栏管理；与 `Architecture.md` 的回收器/延迟释放衔接；按提交粒度回收（Epoch Ring/Overdue）。
- 验收
  - 长时间运行无内存攀升；资源在 fence 达成后回收；背压参数可控。

#### M7 RDG 接口对接（可选，~4–8 天）
- 任务
  - 提供 RDG 需要的 barrier/资源别名/并行录制钩子；Pass DAG 的提交映射。
- 验收
  - RDG 示例 Pass 并行录制/提交正常；自动 barrier 生效。

### 每里程碑 TODO（汇总）
- M0
  - [ ] 重命名/签名修正与编译通过
  - [ ] `DestroySurface/SetCurrentPresentMode` 实现
  - [ ] 能力驱动的格式/模式选择
  - [ ] 移除 Vulkan 常量外泄；去重 `GPUProgramDesc`
- M1
  - [ ] 引入 `RHISwapchain` 类型
  - [ ] `RHIDevice` 管理多交换链
  - [ ] 确认销毁路径/无泄漏
- M2
  - [ ] `RHIQueue/RHISubmitInfo/RHITimelineFence`
  - [ ] Vulkan 映射 timeline semaphore
  - [ ] 提交/等待/查询样例
- M3
  - [ ] `RHIBuffer/RHIImage` + 分配器(VMA)
  - [ ] Barrier/队列所有权
  - [ ] 上传路径与示例
- M4
  - [ ] 描述符布局/集/堆与 PipelineLayout
  - [ ] Bindless（可选）
- M5
  - [ ] ShaderModule + 反射
  - [ ] Graphics/Compute Pipeline + 缓存
- M6
  - [ ] 帧围栏/在飞帧管理
  - [ ] 延迟释放与回收器整合
- M7（可选）
  - [ ] RDG 钩子与自动 barrier 对接

### 风险与注意事项
- 接口迁移影响面广，建议自下而上增量替换，保持旧路径临时适配层。
- TimelineSemaphore/D3D12Fence 语义差异需统一到 `RHITimelineFence`。
- 资源/描述符抽象需考虑 Bindless 与非 Bindless 双路径；特性检测与兼容策略提前定义。
- 多窗口/多队列在一致的回收策略下验证压力场景（频繁 resize/模式切换）。

### 追踪建议
- 每个里程碑产出示例工程（或现有 Samples 改造）作为验收。
- 将本文件与 `Architecture.md` 同步更新：新增接口草图、关键决策记录、已完成标记。


