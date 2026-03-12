# Architecture Overview

Arisen Engine is a next-generation game engine separated into distinct layers to maximize performance, customize-ability, and safety.

## 1. Projects Structure mapping
- **`Core`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\Core`): The C++ layer. Built for absolute maximum performance, hardware interfacing (Vulkan/DirectX), and low-level memory allocation.
- **`Engine`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\Engine`): The C# layer. Contains the primary ECS, game simulation logic, mathematics, and high-level scene management.
- **`BindingGenerator`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\BindingGenerator`): A CLI tool/generator that parses C++ headers and emits C# PInvoke code.
- **`AutoBinding`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\AutoBinding`): The output directory for the Binding Generator. Do NOT hand-edit these files.
- **`Editor`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\Editor`): The Avalonia-based C# Editor. It references the `Engine` project and provides the user interface for scene authoring.
- **`Scripts`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\Scripts`): Build scripts (BAT/Bash) that orchestrate C++ CMake builds and C# MSBuild.
- **`Test`** (`e:\Jingwen\ArisenEngine\Engine\Arisen\Test`): NUnit/xUnit test suites for both Engine logic and Package systems.

## 2. The EngineLifecycle
Managed by `EngineKernel`, not a single `while(true)`. 
- Boots via `IEngineSubsystem` plugins sequentially.
- No loose global state.

## 3. The C++ to C# Bridge
The core philosophy is **C# drives the logic, C++ executes the heavy lifting.**
- C# allocates NativeArrays and Spans.
- C# passes memory pointers to C++ for batch processing (e.g., physics solving, render graph submission).
- Memory ownership is explicit. If C++ allocates it, C++ frees it. If C# allocates via unmanaged APIs, C# must free it.
