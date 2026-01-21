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
build_rhi_test_case_all.bat
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
- 测试用利注册系统
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

## 最佳实践

1. **测试隔离**: 每个测试应该独立，不依赖其他测试的状态
2. **资源清理**: 总是在 `TeardownTest()` 中正确清理资源
3. **正确归类**: 逻辑测试放 `Unit`，涉及屏幕显示放 `Rendering`
4. **使用 Headless**: 尽量使用 `IsHeadless() = true` 提高测试效率

---

**项目状态**: 🚀 准备就绪，可用于清晰、可维护的 RHI 测试！
