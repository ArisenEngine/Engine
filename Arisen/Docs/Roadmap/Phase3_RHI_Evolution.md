# Phase 3: High-Throughput RHI Evolution

**Objective**: Break the single GraphicQueue limitation and embrace GPU-driven rendering techniques.

## 3.1 Multi-Queue RHI Abstraction
The C++ RHI currently assumes a unified Graphics Queue. We need to split Compute and Transfer.

**Implementation Steps:**
1. **Queue Families Fetching:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\RHI.Vulkan\RHIVkDevice.cpp`.
   - Fetch distinct `vkGetDeviceQueue` handles for `Transfer` (for background loads) and `Compute` (for async simulation).
2. **Queue Abstraction:**
   - Update `RHISubmitInfo` and `RHIDevice::Submit` to accept a `CommandListType` enum (`Graphics`, `Compute`, `Transfer`).

## 3.2 Advanced Queue Synchronization
Concurrent queues require explicit memory barriers and execution fences.

**Implementation Steps:**
1. **RHISemaphore Implementation:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Core\Core.RHI\RHISyncPrimitive.h`.
   - Implement a Vulkan Timeline Semaphore wrapper if supported, otherwise binary semaphores.
2. **Cross-Queue Execution Ties:**
   - Allow a `Submit` on the Graphics queue to wait patiently on a Semaphore signaled by the Transfer queue (e.g., waiting for models to finish uploading before drawing).

## 3.3 Next-Gen Deskriptors
Modern Vulkan relies on Descriptor Indexing and Buffers.

**Implementation Steps:**
1. **Descriptor Buffer Support:**
   - Evaluate `VK_EXT_descriptor_buffer`. If capable, implement it inside `RHIVkDevice`.
   - Create C# structures mapping directly to this buffer.
2. **Bindless Table Generation:**
   - Provide a global descriptor pool/buffer containing all textures loaded in the scene, and pass index IDs to shaders via Push Constants.
