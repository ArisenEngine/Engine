namespace ArisenKernel.Contracts;

/// <summary>
/// Contract for the Rendering Hardware Interface Device.
/// </summary>
public interface IRHIDevice
{
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
}
