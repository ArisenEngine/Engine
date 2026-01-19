# RHIUnitTest - RHI 测试框架

RHIUnitTest 是一个模块化、可扩展的 RHI (渲染硬件接口) 测试框架，用于验证 Vulkan/DirectX 渲染功能。

## 项目结构

```
Test/RHIUnitTest/
├── Main.cpp                        # 入口点
├── Framework/
│   └── TestRunner.h                # 测试注册与执行系统
├── RHI/
│   ├── RHITestBase.h               # RHI 测试基类
│   └── RHIBasicRenderingTest.h     # 基础渲染测试示例
├── RHIUnitTest.h                   # 遗留单体测试（保留以确保稳定性）
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
cd Projects\VisualStudio\RHIUnitTest\Outputs\Debug
RHIUnitTest.exe
```

**当前状态**: ✅ 构建成功 | ✅ ~190 FPS | ✅ 无验证错误

## 添加新测试用例

### 1. 创建测试类

在 `Test/RHIUnitTest/RHI/` 目录下创建新的测试文件：

```cpp
// RHIBufferTest.h
#pragma once
#include "RHITestBase.h"

namespace ArisenEngine::Testing {
    class RHIBufferTest : public RHITestBase {
    public:
        const char* GetName() const override { 
            return "RHIBufferTest"; 
        }
        
        bool SetupTest() override {
            // 初始化测试资源
            // 例如：创建缓冲区、分配内存等
            return true;
        }
        
        bool Run() override {
            // 执行测试逻辑
            // 例如：测试缓冲区读写、映射等
            return true;
        }
        
        void TeardownTest() override {
            // 清理测试资源
        }
    };
}
```

### 2. 注册并运行测试（未来迁移）

```cpp
// 在 Main.cpp 中（当前使用遗留测试，未来迁移时使用）
#include "RHI/RHIBufferTest.h"

// 注册测试
TestRunner::RegisterTest<RHIBufferTest>();

// 运行所有测试
auto results = TestRunner::RunAllTests();
```

## 测试框架组件

### TestRunner（测试运行器）

[TestRunner.h](Framework/TestRunner.h) 提供：
- 测试用例注册系统
- 自动执行和结果报告
- 异常处理和日志记录
- 通过/失败状态追踪

```cpp
// 注册一个测试
TestRunner::RegisterTest<MyCustomTest>();

// 运行所有已注册的测试
auto results = TestRunner::RunAllTests();

// 检查结果
for (const auto& result : results) {
    if (!result.passed) {
        LOG_ERROR("Test %s failed: %s", 
                  result.testName.c_str(), 
                  result.errorMessage.c_str());
    }
}
```

### RHITestBase（RHI 测试基类）

[RHITestBase.h](RHI/RHITestBase.h) 处理通用 RHI 设置：
- RHI 实例创建
- 设备和表面初始化
- 窗口管理
- 帧同步

**继承的成员变量**:
- `m_Instance` - RHI 实例句柄
- `m_Device` - 设备句柄
- `m_WindowId` - 窗口 ID
- `m_MaxFramesInFlight` - 最大飞行帧数
- `m_FrameIndex` - 当前帧索引

**可重写的方法**:
- `SetupTest()` - 测试特定的设置
- `Run()` - 测试逻辑执行
- `TeardownTest()` - 测试特定的清理

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

## 示例测试用例

### 基础渲染测试

[RHIBasicRenderingTest.h](RHI/RHIBasicRenderingTest.h) 演示新结构：
- 继承自 `RHITestBase`
- 将测试逻辑组织成专注的方法
- 包含 TODO 标记，用于逐步迁移完整渲染逻辑
- 自包含且可重用

**验证内容**:
- RHI 初始化
- 缓冲区创建（顶点、索引、uniform）
- 图像/纹理创建
- 着色器编译和加载
- 管线状态配置
- 渲染通道和帧缓冲设置
- 命令缓冲区记录和提交
- 交换链呈现

## 最佳实践

1. **测试隔离**: 每个测试应该独立，不依赖其他测试的状态
2. **资源清理**: 总是在 `TeardownTest()` 中正确清理资源
3. **错误处理**: 使用 `LOG_ERROR` 报告失败，并返回 `false`
4. **命名约定**: 测试类名应该清晰描述测试内容（例如 `RHIBufferTest`）
5. **文档注释**: 为测试类添加注释，说明测试验证的内容

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
