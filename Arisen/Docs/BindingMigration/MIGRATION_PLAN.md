# Arisen Engine: CppSharp → 自定义绑定生成器迁移计划

> **创建日期**: 2026-02-26  
> **状态**: 实施中  
> **目标**: 用轻量级自定义生成器替换 CppSharp，消除 ~400 行 regex 后处理，提升可维护性

---

## 目录

1. [背景与动机](#1-背景与动机)
2. [当前架构分析](#2-当前架构分析)
3. [新架构设计](#3-新架构设计)
4. [C++ Core 层 API 审计](#4-c-core-层-api-审计)
5. [Phase 0: 宏系统 + 基础设施](#phase-0-宏系统--基础设施)
6. [Phase 1: 已有 extern C API 迁移](#phase-1-已有-extern-c-api-迁移)
7. [Phase 2: 枚举 + 结构体生成](#phase-2-枚举--结构体生成)
8. [Phase 3: 类桥接函数](#phase-3-类桥接函数)
9. [Phase 4: 清理 + 高级特性](#phase-4-清理--高级特性)
10. [类型映射表](#类型映射表)
11. [验证计划](#验证计划)

---

## 1. 背景与动机

### 当前问题

`BindingGenerator/Program.cs` 中的 `PostProcessGeneratedCode` 方法有 ~400 行 regex hack：

| 问题类别 | 行数 | 说明 |
|---------|------|------|
| 命名空间扁平化 | ~80 | 平衡大括号正则, `ArisenBinding` → `Arisen.Native` |
| DllImport 映射 | ~15 | 按文件名硬编码匹配 DLL 名 |
| 结构体手动注入 | ~200 | `RHIHandle`, `RHIBufferDescriptor` 等属性 |
| 字符串/Marshalling | ~40 | `std::string` → `IntPtr`, UTF8Marshaller |
| STL 类型清理 | ~20 | `Std.*` → `System.IntPtr` |
| 杂项修复 | ~40 | `__Internal` 可见性, `Dispose` 冲突 |

**根因**: CppSharp 对模板(`RHIHandle<T>`)、深层命名空间、`uint32_t` 属性支持不佳。

### 方案选型结论

| 方案 | 模板支持 | 后处理需求 | 可维护性 | 结论 |
|------|---------|-----------|---------|------|
| CppSharp | ❌ 很差 | ~400行regex | ❌ | 弃用 |
| SWIG | ⚠️ 需手动实例化 | typemap文件 | ⚠️ | 不采用 |
| **自定义生成器** | ✅ 自行定义 | **零** | ✅ | **采用** |

---

## 2. 当前架构分析

### 构建流水线 (CMakeLists.txt L195-246)

```
C++ 编译 (Core.Foundation → Core.RHI → RHI.Vulkan/DX12)
    ↓
BuildBindingGenerator (dotnet build BindingGenerator.csproj, 依赖 CppSharp NuGet)
    ↓
GenerateAutoBinding (BindingGenerator.exe --source_code ... , CppSharp 解析 C++ 头文件 → 生成 C#)
    ↓  
PostProcessGeneratedCode (~400 行 regex 修复生成的代码)
    ↓
BuildAutoBinding (dotnet build AutoBinding.csproj, 运行时依赖 CppSharp)
    ↓
BuildArisenEngine (dotnet build ArisenEngine.csproj)
    ↓
BuildCSharpEngineTest / BuildArisenEditor
```

### 关键文件

| 文件 | 用途 | 行数 |
|------|------|------|
| `BindingGenerator/Program.cs` | 主程序：调用 CppSharp + 后处理 | 635 |
| `BindingGenerator/ArisenLibrary.cs` | CppSharp 基类配置（MSVC/SDK 路径） | 90 |
| `BindingGenerator/Modules/ArisenUnifiedLibrary.cs` | CppSharp 模块配置（头文件白名单） | 192 |
| `BindingGenerator/Passes/*.cs` | CppSharp AST passes | 2文件 |
| `BindingGenerator/TypeMaps/*.cs` | CppSharp type maps | 2文件 |
| `AutoBinding/*.cs` | 生成的 32 个 C# 文件 | ~500KB |

---

## 3. 新架构设计

### 核心思路

```
C++ Header (标注了宏) → 文本扫描器 → 干净的 C# P/Invoke 代码
```

### 新构建流水线

```
C++ 编译 (和以前完全一样, + Bridges/*.cpp 编入 DLL)
    ↓
BuildBindingGenerator (dotnet build, 无 CppSharp 依赖)
    ↓
GenerateAutoBinding (纯文本扫描标注宏, 直接生成 C#)
    ↓  (无后处理步骤)
BuildAutoBinding (dotnet build, 无 CppSharp 运行时依赖)
    ↓
BuildArisenEngine → 测试/编辑器
```

### CMake 依赖链 — 完全不变

`CMakeLists.txt` 中的 `add_custom_target` 和 `add_dependencies` 逻辑不需要任何修改。

### 项目结构变化

```
Arisen/
├── Core/
│   ├── Core.Foundation/
│   │   ├── Base/
│   │   │   ├── BindingMacros.h          ← [NEW] 宏定义
│   │   │   └── ... (不变)
│   │   └── CMakeLists.txt               ← [MOD] 添加 Bridges/*.cpp
│   ├── Core.Diagnostic/
│   │   ├── Bridges/                     ← [NEW]
│   │   │   └── LoggerBridge.cpp
│   │   └── CMakeLists.txt               ← [MOD]
│   ├── Core.HAL/                        ← (RenderWindowAPI 已是 extern C, 几乎不变)
│   ├── Core.RHI/
│   │   ├── Bridges/                     ← [NEW] 最大桥接目录
│   │   │   ├── RHIDeviceBridge.cpp
│   │   │   ├── RHIInstanceBridge.cpp
│   │   │   ├── RHIFactoryBridge.cpp
│   │   │   ├── RHICommandBufferBridge.cpp
│   │   │   └── ...
│   │   ├── RHI/Handles/RHIHandleInterop.h  ← [NEW] 非模板 RHIHandle
│   │   └── CMakeLists.txt               ← [MOD]
│   └── Core.ShaderCompiler/             ← (已是 extern C, 几乎不变)
├── BindingGenerator/                    ← [大幅简化]
│   ├── Program.cs                       ← [REWRITE] ~200 行纯文本解析
│   ├── BindingGenerator.csproj          ← [MOD] 移除 CppSharp
│   ├── Modules/                         ← [DELETE]
│   ├── Passes/                          ← [DELETE]
│   └── TypeMaps/                        ← [DELETE]
├── AutoBinding/                         ← [输出更干净]
│   ├── *.cs                             ← 自动生成
│   └── AutoBinding.csproj               ← [MOD] 移除 CppSharp
└── Engine/                              ← [完全不变]
```

---

## 4. C++ Core 层 API 审计

### 模块概览

| 模块 | DLL | 需绑定的 API | 复杂度 |
|------|-----|-------------|-------|
| Core.Foundation | `Core.Foundation.dll` | 2 extern 函数 + `ILogHandler` + `LogLevel` 枚举 + `LogSourceLocation` 结构体 | 低 |
| Core.Diagnostic | `Core.Diagnostic.dll` | `Logger` 单例(5方法) + `LogCallback` delegate | 低 |
| Core.HAL | `Core.HAL.dll` | 12 `extern "C"` 函数 + `EngineInit` 2静态方法 | 低 |
| Core.ShaderCompiler | `Core.ShaderCompiler.dll` | 2 `extern "C"` 函数 + 2结构体 | 中 |
| Core.RHI | `Core.RHI.dll` | ~59枚举 + 3结构体描述符 + 5核心类(~130方法) | **高** |

### RHI 核心类方法统计

| 类名 | public 方法数 | 特征 | DLL导出 |
|------|-------------|------|---------|
| `RHILoader` | 3 | 静态方法 | ✅ `RHI_DLL` |
| `RHIInstance` | ~20 | 虚函数 | ✅ `RHI_DLL` |
| `RHIDevice` | ~30 | 虚函数 | ✅ `RHI_DLL` |
| `RHIFactory` | ~30 | 纯虚接口(无DLL导出) | ❌ 通过 `RHIDevice::GetFactory()` 访问 |
| `RHICommandBuffer` | ~40 | 命令录制 | ✅ `RHI_DLL` |
| `RHICommandBufferPool` | ~5 | 命令缓冲管理 | ✅ `RHI_DLL` |
| `RHIDescriptorPool` | ~8 | 描述符集管理 | ✅ `RHI_DLL` |
| `RHIDescriptorSet` | ~6 | 描述符绑定 | ✅ `RHI_DLL` |
| `RHISampler` | ~3 | POD 描述符 | ✅ |
| `RHIFence/Semaphore` | ~5 | 同步原语 | ✅ |

### RHI 枚举分布 (~59个)

| 子目录 | 枚举数 | 示例 |
|--------|-------|------|
| `Enums/Image/` | 11 | `EFormat`, `EImageLayout`, `ESampleCountFlagBits` |
| `Enums/Pipeline/` | 22 | `EProgramStage`, `ECullMode`, `EBlendFactor` |
| `Enums/Memory/` | 5 | `ESharingMode`, `ERHIMemoryUsage` |
| `Enums/Buffer/` | 2 | `EBufferUsage`, `EBufferCreateFlagBits` |
| `Enums/Sampler/` | 5 | `ECompareOp`, `EFilter` |
| `Enums/Attachment/` | 3 | `EAttachmentLoadOp`, `EAttachmentStoreOp` |
| `Enums/Subpass/` | 3 | `ESubpassContents`, `EDependencyFlag` |
| `Enums/Swapchain/` | 3 | `EPresentMode` |
| `Enums/RayTracing/` | 5 | RT 相关 |

### C++ 模式对绑定的影响

| 模式 | 位置 | 影响 | 解决方案 |
|------|------|------|---------|
| 模板 `RHIHandle<T>` | `RHIHandle.h` | 16个typedef别名 | 创建 `RHIHandleInterop` 非模板版 |
| 虚函数/抽象类 | RHIDevice 等 | 无法直接P/Invoke | extern C 桥接函数 |
| 自定义 `String` | `String.h` | 内部 std::string | 跨边界传 `const char*` |
| `extern "C"` | HAL, ShaderCompiler | ✅ 已可P/Invoke | 直接标注即可 |
| `Containers::Vector` | CommandBuffer | STL类型 | 指针+长度替代 |
| DLL导出宏 | 每个模块 | 已有体系 | Bridge 文件复用 |

---

## Phase 0: 宏系统 + 基础设施

**预计时间**: 1-2 天  
**目标**: 建立绑定宏体系，不破坏任何现有代码

### 0.1 创建 BindingMacros.h

**路径**: `Core/Core.Foundation/Base/BindingMacros.h`

```cpp
#pragma once

// ============================================================================
// Arisen Engine Binding Annotation Macros
// ============================================================================
// 这些宏在 C++ 编译时展开为空，不影响编译。
// 它们仅供绑定生成器(BindingGenerator)文本扫描使用。
//
// 用法示例:
//   ARISEN_BIND_MODULE("Core.RHI.dll")
//   ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")
//   ARISEN_BIND_ENUM(EFormat)
//   enum class EFormat : uint32_t { ... };
// ============================================================================

// 指定当前文件中 extern "C" 函数所属的 DLL
#define ARISEN_BIND_MODULE(dll_name)

// 指定生成的 C# 代码的命名空间
#define ARISEN_BIND_NAMESPACE(ns)

// 标记一个 enum 需要生成到 C#
#define ARISEN_BIND_ENUM(name)

// 标记一个 blittable 结构体需要生成到 C#
#define ARISEN_BIND_STRUCT(name)

// 标记一组 extern "C" 桥接函数的开始
// class_name: 生成的 C# 静态类名后缀 (如 "RHIDevice" → RHIDeviceAPI)
// dll_name: DllImport 的 DLL 名
// cs_namespace: C# 命名空间
#define ARISEN_BIND_BEGIN_BRIDGE(class_name, dll_name, cs_namespace)

// 标记桥接函数组的结束
#define ARISEN_BIND_END_BRIDGE()
```

### 0.2 修改 FoundationMinimal.h

在 `#include "Assertion.h"` 之后添加:

```cpp
#include "BindingMacros.h"
```

### 0.3 验证

- C++ 编译不受影响（宏展开为空）
- 现有 build_csharp_engine_test_all.bat 正常通过

---

## Phase 1: 已有 extern C API 迁移

**预计时间**: 1-2 天  
**目标**: 迁移最简单的已有 `extern "C"` API，编写生成器 v1

### 1.1 标注已有 extern C 函数

#### Core.Foundation — Assertion.h (2 个函数) 
ARISEN_BIND_NAMESPACE("Arisen.Native.HAL")
```

#### Core.ShaderCompiler — ShaderCompilerAPI.h (2 个函数)

```cpp
ARISEN_BIND_MODULE("Core.ShaderCompiler.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.ShaderCompiler")
```

### 1.2 编写生成器 v1 (Program.cs 重写)

新版 `BindingGenerator/Program.cs` 核心逻辑:

```
1. 递归扫描 --source_code 目录下所有 .h 文件
2. 跳过不含 ARISEN_BIND_ 标注的文件
3. 提取 ARISEN_BIND_MODULE("xxx") → DLL 名
4. 提取 ARISEN_BIND_NAMESPACE("xxx") → C# 命名空间
5. 匹配 extern "C" { ... } 块中的函数签名 → 生成 [DllImport]
6. 输出到 --output 目录
```

正则模式:
```
函数签名: (?:XXX_DLL\s+)?(\w[\w\s\*&]*?\s+)(\w+)\s*\(([^)]*)\)\s*;
参数:      (\w[\w\s\*&]*?\s+)(\w+)
```

类型映射: 参见 [类型映射表](#类型映射表)

### 1.3 修改 BindingGenerator.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net9.0</TargetFramework>
        <!-- 不再需要 CppSharp -->
    </PropertyGroup>
</Project>
```

### 1.4 验证

- 生成的 `RenderWindowAPI.cs` 与当前版本功能等价
- `dotnet build AutoBinding.csproj` 通过
- `build_csharp_engine_test_all.bat` 正常

---

## Phase 2: 枚举 + 结构体生成

**预计时间**: 2-3 天  
**目标**: 自动生成 RHI 的 ~59 个枚举和核心结构体

### 2.1 枚举标注

在每个枚举头文件中添加标注宏。示例:

```cpp
// Enums/Image/EFormat.h
ARISEN_BIND_ENUM(EFormat)
enum class EFormat : uint32_t { UNDEFINED = 0, R8_UNORM = 1, ... };
```

需要标注的文件列表:
- `Enums/Image/`: EFormat, EImageLayout, EImageType, EImageTiling, EImageViewType, 
  ESampleCountFlagBits, EImageUsageFlagBits, EImageAspectFlagBits, EColorSpace, 
  EComponentSwizzle, ECompositeAlphaFlagBits
- `Enums/Pipeline/`: EProgramStage, ECullMode, EFrontFace, EPrimitiveTopology, 
  EBlendFactor, EBlendOp, EColorComponentFlag, EPipelineStageFlag, EPipelineBindPoint,
  EIndexType, ECommandBufferUsageFlagBits, ECommandBufferLevel, EAccessFlag,
  EStencilOp, EDynamicState, EVertexInputRate, EPolygonMode, ELogicOp,
  EShadingRate, EShadingRateCombiner, EShaderStageFlag, ERenderPassScope
- `Enums/Memory/`: ESharingMode, ERHIMemoryUsage, EMemoryPropertyFlagBits, 
  EMemoryViewType, EImageViewCreateFlagBits
- `Enums/Buffer/`: EBufferUsage, EBufferCreateFlagBits
- `Enums/Sampler/`: ECompareOp, EFilter, ESamplerAddressMode, ESamplerMipmapMode, EBorderColor
- `Enums/Attachment/`: EAttachmentLoadOp, EAttachmentStoreOp, EAttachmentDescFlagBits
- `Enums/Subpass/`: ESubpassContents, EDependencyFlag, ESubpassDescriptionFlagBits
- `Enums/Swapchain/`: EPresentMode + 其他

### 2.2 结构体标注 + RHIHandleInterop

#### 创建 `RHIHandleInterop.h`

```cpp
// Core.RHI/RHI/Handles/RHIHandleInterop.h
#pragma once
#include <cstdint>
#include "Base/BindingMacros.h"
#include "RHIHandle.h"

ARISEN_BIND_MODULE("Core.RHI.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.RHI")

ARISEN_BIND_STRUCT(RHIHandle)
struct RHIHandleInterop {
    uint32_t index;
    uint32_t generation;
};

// 保证二进制兼容
static_assert(sizeof(RHIHandleInterop) == sizeof(ArisenEngine::RHI::RHIHandle<ArisenEngine::RHI::RHIBufferTag>));
```

#### 标注其他结构体

| 结构体 | 头文件 | 字段数 |
|--------|-------|-------|
| `RHIBufferDescriptor` | `RHIResourceDescriptors.h` | 7 |
| `RHIImageDescriptor` | `RHIResourceDescriptors.h` | 13 |
| `RHIImageViewDesc` | `RHIResourceDescriptors.h` | 9 |
| `RHIInstanceInfo` | `RHIInstance.h` | 13 |
| `LogSourceLocation` | `ILogHandler.h` | 3 |
| `RHIDeviceLimits` | `DeviceLimits.h` | ~20+ |

### 2.3 生成器 v2 新增功能

```
枚举解析正则:
  ARISEN_BIND_ENUM\(\s*(\w+)\s*\)
  enum\s+class\s+(\w+)\s*:\s*(\w+)\s*\{([^}]+)\}

结构体解析:
  ARISEN_BIND_STRUCT\(\s*(\w+)\s*\)
  struct\s+(\w+)\s*\{([^}]+)\}
  逐行解析字段: (\w[\w:]*)\s+(\w+)(?:\s*=\s*[^;]+)?;
```

### 2.4 验证

- 对比枚举值与 CppSharp 生成版本
- `StructLayout(LayoutKind.Sequential)` 对齐正确
- `dotnet build` 通过

---

## Phase 3: 类桥接函数

**预计时间**: 3-5 天  
**目标**: 为所有需暴露给 C# 的 C++ 类方法编写 `extern "C"` 桥接

### 优先级

#### P0 — 引擎初始化链路 (先通)

| 桥接目标 | 方法数 | 文件 |
|---------|-------|------|
| `EngineInit` | 2 | `Core.HAL/Bridges/EngineInitBridge.cpp` |
| `Logger` | 5 | `Core.Diagnostic/Bridges/LoggerBridge.cpp` |
| `RHILoader` | 3 | `Core.RHI/Bridges/RHILoaderBridge.cpp` |

#### P1 — RHI 核心对象

| 桥接目标 | 方法数 | 文件 |
|---------|-------|------|
| `RHIInstance` | ~20 | `Core.RHI/Bridges/RHIInstanceBridge.cpp` |
| `RHIDevice` | ~15 | `Core.RHI/Bridges/RHIDeviceBridge.cpp` |
| `RHIFactory` | ~30 | `Core.RHI/Bridges/RHIFactoryBridge.cpp` |

#### P2 — 命令录制

| 桥接目标 | 方法数 | 文件 |
|---------|-------|------|
| `RHICommandBuffer` | ~40 | `Core.RHI/Bridges/RHICommandBufferBridge.cpp` |
| `RHICommandBufferPool` | ~5 | `Core.RHI/Bridges/RHICommandBufferPoolBridge.cpp` |

#### P3 — 同步 + 描述符

| 桥接目标 | 方法数 | 文件 |
|---------|-------|------|
| `RHIFence/Semaphore` | ~5 | `Core.RHI/Bridges/RHISyncBridge.cpp` |
| `RHIDescriptorPool/Set` | ~14 | `Core.RHI/Bridges/RHIDescriptorBridge.cpp` |
| `RHISampler` | ~3 | `Core.RHI/Bridges/RHISamplerBridge.cpp` |

### 桥接编写模式

```cpp
// 文件: Core.RHI/Bridges/RHIDeviceBridge.cpp
#include "RHI/Core/RHIDevice.h"
#include "RHI/Handles/RHIHandleInterop.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_BEGIN_BRIDGE("RHIDevice", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

RHI_DLL void RHIDevice_DeviceWaitIdle(RHIDevice* device) {
    device->DeviceWaitIdle();
}

RHI_DLL uint32_t RHIDevice_GetMaxFramesInFlight(RHIDevice* device) {
    return device->GetMaxFramesInFlight();
}

RHI_DLL void RHIDevice_SetResolution(RHIDevice* device, uint32_t w, uint32_t h) {
    device->SetResolution(w, h);
}

RHI_DLL RHIFactory* RHIDevice_GetFactory(RHIDevice* device) {
    return device->GetFactory();
}

// ... 其余方法依此模式 ...

} // extern "C"

ARISEN_BIND_END_BRIDGE()
```

### 生成器 v3 新增功能

解析 `ARISEN_BIND_BEGIN_BRIDGE(...)` / `ARISEN_BIND_END_BRIDGE()` 块:
```
1. 提取 class_name, dll_name, cs_namespace
2. 在块内查找 extern "C" 函数签名
3. 生成 C# 静态类 + [DllImport] 声明
```

### CMakeLists.txt 修改

每个模块添加 Bridges 源文件:
```cmake
# Core.RHI/CMakeLists.txt
file(GLOB_RECURSE RHI_SOURCES "RHI/**/*.cpp" "Bridges/*.cpp" "Source.cpp")
```

### 验证

逐步替换：RHILoader → RHIInstance → RHIDevice → RHIFactory → RHICommandBuffer  
每步运行 `build_csharp_engine_test_all.bat` 验证

---

## Phase 4: 清理 + 高级特性

**预计时间**: 2-3 天

### 4.1 删除 CppSharp 依赖

- `BindingGenerator.csproj`: 移除 `<PackageReference Include="CppSharp" />`
- `AutoBinding.csproj`: 移除 `<PackageReference Include="CppSharp" />`
- 删除: `BindingGenerator/Modules/`, `Passes/`, `TypeMaps/`, `ArisenLibrary.cs`
- 删除: `AutoBinding/ArisenBinding-symbols.cpp`, `Std-symbols.cpp`, `Std.cs`

### 4.2 Callback/Delegate 支持

| Callback | C++ 签名 | C# delegate |
|----------|---------|-------------|
| `LogCallback` | `void(*)(UInt32, const char*, const char*, const char*)` | `delegate void LogCallback(uint level, string msg, string file, string func)` |
| `WindowProc` | `LRESULT(HWND, UINT, WPARAM, LPARAM)` | `delegate IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)` |
| `WindowResize` | `void(*)(uint32_t, uint32_t)` | `delegate void WindowResize(uint w, uint h)` |

### 4.3 可选: 增量生成

- 检测头文件修改时间戳
- 支持 `--dry-run` 模式

---

## 类型映射表

### 基础类型 (C++ → C#)

| C++ 类型 | C# 类型 | 说明 |
|---------|--------|------|
| `void` | `void` | |
| `bool` | `bool` | 1字节 |
| `uint8_t` / `UInt8` | `byte` | |
| `int8_t` / `SInt8` | `sbyte` | |
| `uint16_t` / `UInt16` | `ushort` | |
| `int16_t` / `SInt16` | `short` | |
| `uint32_t` / `UInt32` | `uint` | |
| `int32_t` / `SInt32` | `int` | |
| `uint64_t` / `UInt64` | `ulong` | |
| `int64_t` / `SInt64` | `long` | |
| `float` / `Float32` | `float` | |
| `double` | `double` | |
| `size_t` / `SIZE_T` | `nuint` | 平台相关 |

### 指针类型

| C++ 类型 | C# 类型 | 说明 |
|---------|--------|------|
| `void*` | `IntPtr` | |
| `T*` (任意类指针) | `IntPtr` | 不透明句柄 |
| `const char*` | `[MarshalAs(UnmanagedType.LPUTF8Str)] string` | UTF-8 字符串 |
| `const wchar_t*` | `[MarshalAs(UnmanagedType.LPWStr)] string` | UTF-16 字符串 |
| `T**` | `ref IntPtr` 或 `IntPtr*` | unsafe |

### 自定义类型

| C++ 类型 | C# 类型 | 说明 |
|---------|--------|------|
| `RHIHandleInterop` | `RHIHandle` | 8字节 blittable struct |
| `RHIDevice*` | `IntPtr` | 不透明句柄 |
| 所有 `enum class E : uint32_t` | `enum E : uint` | 直接映射 |
| POD struct (标注了) | `[StructLayout] struct` | 逐字段映射 |

---

## 验证计划

### 每阶段验证

| 阶段 | 验证方式 | 命令 |
|------|---------|------|
| Phase 0 | C++ 编译 | CMake build (确认宏无编译影响) |
| Phase 1 | 生成 RenderWindowAPI.cs 对比 | diff + `dotnet build` |
| Phase 2 | 枚举和结构体对比 | 脚本比较 CppSharp 输出 |
| Phase 3-P0 | 引擎初始化 | `build_csharp_engine_test_all.bat` |
| Phase 3-P1 | RHI 实例创建 | CSharpEngineTest 窗口显示 |
| Phase 3-P2 | 渲染命令 | CSharpEngineTest 正常渲染 |
| Phase 4 | 全流程无 CppSharp | 完整 build + 运行 |

### 最终验证

```bash
# 1. C++ 编译无错误
cmake --build ... --config Debug
cmake --build ... --config Release

# 2. 绑定生成器成功
BindingGenerator.exe --source_code ... --output ...

# 3. C# 编译无错误
dotnet build AutoBinding.csproj
dotnet build ArisenEngine.csproj

# 4. 测试运行
CSharpEngineTest.exe  # 正常渲染
```

---

## 附录: 重要文件路径速查

| 用途 | 路径 |
|------|------|
| 主 CMakeLists | `Arisen/CMakeLists.txt` |
| 构建脚本 | `Arisen/Scripts/Windows/Test/build_csharp_engine_test_all.bat` |
| BindingGenerator 入口 | `Arisen/BindingGenerator/Program.cs` |
| AutoBinding 输出 | `Arisen/AutoBinding/*.cs` |
| C# Engine | `Arisen/Engine/` |
| C++ Foundation | `Arisen/Core/Core.Foundation/` |
| C++ Diagnostic | `Arisen/Core/Core.Diagnostic/` |
| C++ HAL | `Arisen/Core/Core.HAL/` |
| C++ RHI | `Arisen/Core/Core.RHI/` |
| C++ ShaderCompiler | `Arisen/Core/Core.ShaderCompiler/` |
| RHI 枚举 | `Arisen/Core/Core.RHI/RHI/Enums/` (9 subdirs, ~59 files) |
| RHI 核心类 | `Arisen/Core/Core.RHI/RHI/Core/` (RHIDevice, RHIInstance, RHIFactory) |
| RHI Handle | `Arisen/Core/Core.RHI/RHI/Handles/RHIHandle.h` |
