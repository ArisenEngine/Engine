# RHITestCase - RHI 测试框架

RHITestCase 是一个模块化、可扩展的 RHI (渲染硬件接口) 测试框架，用于验证 Vulkan/DirectX 渲染功能。

## 项目结构

```
Test/RHITestCase/
├── Main.cpp                        # 入口点
├── Framework/
│   └── TestRunner.h                # 测试注册与执行系统
├── RHI/
│   ├── RHITestBase.h               # RHI 测试基类
│   ├── Unit/                       # 单元测试 (Headless)
│   │   ├── RHISyncTest.h
│   │   └── RHIBindlessTest.h
│   └── Rendering/                  # 渲染测试 (Windowed)
│       ├── RHIBasicRenderingTest.h
│       └── RHIDynamicRenderingTest.h
├── Assets/                         # 测试资源
└── Shader/                         # 测试着色器
```

## 快速开始

### 构建项目

```batch
cd Arisen\Scripts\Windows\Test
build_rhi_unit_test.bat
```

### 运行测试

```batch
cd Projects\VisualStudio\RHITestCase\Outputs\Debug
RHITestCase.exe            # 运行所有测试
RHITestCase.exe --unit      # 仅运行单元测试
RHITestCase.exe --rendering # 仅运行渲染测试
```

**当前状态**: ✅ 构建成功 | ✅ ~190 FPS | ✅ 无验证错误

## 添加新测试用例

### 1. 创建测试类

在 `Test/RHITestCase/RHI/Unit/` 或 `Test/RHITestCase/RHI/Rendering/` 目录下创建新的测试文件：

```cpp
// RHIExampleTest.h
#pragma once
#include "../RHITestBase.h"

namespace ArisenEngine::Testing {
    class RHIExampleTest : public RHITestBase {
    public:
        const char* GetName() const override { return "RHIExampleTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }
        
        bool SetupTest() override { return true; }
        bool Run() override { return true; }
        void TeardownTest() override {}
    };
}
```

### 2. 注册并运行测试

```cpp
// 在 Main.cpp 中
#include "RHI/Unit/RHIExampleTest.h"

// 注册测试
TestRunner::RegisterTest<RHIExampleTest>();
```

## 测试框架组件

### TestRunner（测试运行器）

[TestRunner.h](Framework/TestRunner.h) 提供：
- 测试用例注册系统
- 自动执行和结果报告
- 异常处理和日志记录
- 通过/失败状态追踪
- 类别过滤 (`--unit`, `--rendering`)

### RHITestBase（RHI 测试基类）

[RHITestBase.h](RHI/RHITestBase.h) 处理通用 RHI 设置：
- RHI 实例创建
- 设备管理
- 窗口管理 (可选，通过 `IsHeadless()`)
- 帧同步

### EngineInit（引擎初始化模块）

引擎层的崩溃安全日志关闭模块，位于 `Engine/NativeEngine/Core/EngineInit.h`。

**功能**:
- 集中式崩溃处理（所有应用程序可用）
- 自动日志关闭（崩溃、信号、异常时）
- Windows SEH 平台特定支持

```cpp
// 在任何应用程序中使用：
if (!ArisenEngine::Core::EngineInit::Initialize()) {
    return 1;
}
// ... 你的代码 ...
ArisenEngine::Core::EngineInit::Shutdown();
```

## 迁移策略

### 当前状态

遗留的 `RHIUnitTest.h` 文件仍然保持功能正常。可以逐步迁移测试：

1. **添加新测试**: 所有新测试用例使用模块化框架
2. **提取组件**: 逐步将逻辑从 `RHIUnitTest.h` 移至模块化测试
3. **淘汰遗留代码**: 所有测试迁移完成后，移除 `RHIUnitTest.h`

### 建议的迁移顺序

1. **着色器编译测试** - 提取到专用测试
2. **缓冲区操作** - 创建 `RHIBufferTest`
3. **管线测试** - 创建 `RHIPipelineTest` 用于 PSO 验证
4. **描述符集测试** - 创建 `RHIDescriptorTest`
5. **渲染通道测试** - 创建 `RHIRenderPassTest`

## 最佳实践

1. **测试隔离**: 每个测试应该独立，不依赖其他测试的状态
2. **资源清理**: 总是在 `TeardownTest()` 中正确清理资源
3. **正确归类**: 逻辑测试放 `Unit`，涉及屏幕显示放 `Rendering`
4. **使用 Headless**: 尽量使用 `IsHeadless() = true` 提高测试效率

## 调试技巧

- 使用 `LOG_INFO`, `LOG_DEBUG`, `LOG_ERROR` 进行日志记录
- 在 `SetupTest()` 中验证资源创建
- 检查 `log.log` 文件中的 Vulkan 验证错误
- 使用 RenderDoc 或其他图形调试工具进行深度分析

## 相关文件

- [EngineInit.h](../../Engine/NativeEngine/Core/EngineInit.h) - 引擎初始化模块
- [TestRunner.h](Framework/TestRunner.h) - 测试框架
- [RHITestBase.h](RHI/RHITestBase.h) - RHI 测试基类
- [RHIBasicRenderingTest.h](RHI/RHIBasicRenderingTest.h) - 示例测试

---

**项目状态**: 🚀 准备就绪，可用于清晰、可维护的 RHI 测试！
