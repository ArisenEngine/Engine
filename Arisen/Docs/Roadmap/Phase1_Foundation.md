# Phase 1: Robust Foundation & Capabilities

**Objective**: Stabilize the foundation systems and abstract RHI hardware capabilities robustly before building complex logic.

## 1.1 RHI Capability System
The capability system requires creating a clear `struct` in C++ that queries Vulkan properties during device creation.

**Implementation Steps:**
1. **Define `RHICapabilities` Struct:** [x]
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\Core.RHI\RHIDevice.h` (or a dedicated `RHICapabilities.h`).
   - Add fields like `bool supportsDynamicRendering;`, `uint32_t maxDescriptorSets;`, etc.
2. **Query Vulkan Properties:** [x]
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\RHI.Vulkan\RHIVkDevice.cpp` inside `RHIVkDevice::Init()`.
   - Call `vkGetPhysicalDeviceProperties` and `vkGetPhysicalDeviceFeatures2`.
3. **C# AutoBinding Extraction:** [x]
   - **Path**: Ensure `BindingGenerator` processes `RHICapabilities`.
   - Verify C# can call `RHIDevice.GetCapabilities()`.

## 1.2 Vulkan Extensions & Graceful Fallbacks
Currently, Vulkan initialization relies on hardcoded arrays.

**Implementation Steps:**
1. **Refactor Initialization Settings:** [x]
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\RHI.Vulkan\VulkanInit.cpp` (or wherever instance/device creation occurs).
   - Create `VulkanInitSettings` struct allowing explicit toggling of Validation Layers.
2. **Conditional Validation Layers:** [x]
   - Load `VK_LAYER_KHRONOS_validation` only if `ARISEN_DEBUG` or via a specific config flag.
3. **Dynamic Extensions Loading:** [x]
   - Use `volk` or manual fetching to dynamically map extensions instead of hard failing if an optional extension is missing.

## 1.3 Diagnostics & Profiling Validation
Ensure Tracy is fully operational across the C++ and C# divide.

**Implementation Steps:**
1. **Propagate Tracy Context:** [x]
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\Core.Diagnostic\Profiler.h` and `.cpp`.
   - Verify `RHICommandBuffer::BeginMarker` uses Tracy Vulkan contexts properly.
2. **C# Profiling Hooks:** [x]
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Core\Diagnostics\Profiler.cs`.
   - Ensure the C# wrappers map faithfully to the C++ macros.
3. **Zero Validation Errors Check:** [x]
   - **Path**: Run `d:\EngineSource\ArisenEngine\Engine\Arisen\Test\NativeEngineTest` and confirm a clean Vulkan debug output.
