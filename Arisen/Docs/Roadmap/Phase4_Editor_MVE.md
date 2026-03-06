# Phase 4: Editor MVE (Minimum Viable Editor)

**Objective**: Verify the full data loop: **UI -> Serialization -> C# Engine -> C++ RHI**.

## 4.1 Shell & Viewport Integration
Bring Avalonia and Vulkan together.

**Implementation Steps:**
1. **Native Control Host:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Editor\ArisenEditor\Controls\VulkanViewport.cs` (or similar).
   - Use Avalonia's `NativeControlHost` to obtain the platform `HWND`/`WindowId`.
2. **Swapchain Bind:**
   - Pass the HWND into `RHIDevice::CreateSwapChain` initializing the Vulkan surface directly over the Avalonia control area.

## 4.2 Property Synchronization
Prove that Avalonia modifications reach the Engine instantly.

**Implementation Steps:**
1. **Reflection Property Grid:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Editor\ArisenEditor\Inspector\`.
   - Parse C# structs/classes with a specific attribute (e.g. `[ArisenProperty]`) and generate text fields/color pickers in Avalonia.
2. **Live Tweaking:**
   - Change a parameter via the UI (like Clear Color), which modifies the C# Engine State. The Engine loop subsequently submits this new state to the C++ RHI for the next frame.

## 4.3 Serialization V1
Basic project setting persistence.

**Implementation Steps:**
1. **Text Serialization:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Serialization\`.
   - Implement basic `Project.json` parsing. Ensure the EngineKernel loads this file during `PostInit` before firing the first frame.

## 4.4 Editor Infrastructure & Graphing
Adopt shared infrastructure to accelerate the construction of engine editor tools.

**Implementation Steps:**
1. **Adopt Extensible Frameworks:**
   - Leverage **`ArisenEditorFramework`** to construct the base architecture of the editor, mirroring the systems built for Arisen Studio.
2. **Visual Graph Editing:**
   - Integrate **`ArisenDAG`** as the general backend logic for node graphs.
   - Utilize **`ArisenNodeCanvas`** for rendering node-based editors, enabling features such as an upcoming RenderGraph designer or visual scripting.
