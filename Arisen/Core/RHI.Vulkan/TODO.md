
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
