
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
