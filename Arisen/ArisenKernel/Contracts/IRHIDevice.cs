namespace ArisenKernel.Contracts;

/// <summary>
/// Low-level abstraction representing the underlying Graphics API (Vulkan, DX12, etc).
/// </summary>
[ServiceContract("RHI Device", "The low-level hardware abstraction layer representing the Graphics API (Vulkan, DX12, etc).")]
public interface IRHIDevice
{
    IntPtr NativeHandle { get; }

    bool IsValid { get; }
    
    /// <summary>
    /// Blocks the CPU until the GPU has finished all active work.
    /// </summary>
    void WaitIdle();

    /// <summary>
    /// Submits a command buffer (passed as an opaque pointer/handle) to the GPU queue.
    /// </summary>
    ulong SubmitCommandList(IntPtr commandBufferHandle);

    /// <summary>
    /// Waits for a specific submission ticket on the CPU.
    /// </summary>
    void WaitQueueTicket(ulong ticket);
    
    /// <summary>
    /// Gets the highest ticket number that has completed on the GPU.
    /// </summary>
    ulong GetCompletedTicket();

    /// <summary>
    /// Gets the shared Win32 handle for an exported image, if supported.
    /// </summary>
    IntPtr GetSharedWin32Handle(uint index, uint generation);
}
