namespace ArisenKernel.Contracts;

/// <summary>
/// Contract for the hardware resource creation factory.
/// </summary>
public interface IRHIFactory
{
    bool IsValid { get; }
    
    /// <summary>
    /// Creates a generic GPU buffer. Returns an opaque handle to the internal object.
    /// </summary>
    IntPtr CreateBuffer(ulong size, string name = "");

    /// <summary>
    /// Releases an opaque buffer handle.
    /// </summary>
    void ReleaseBuffer(IntPtr handle);

    /// <summary>
    /// Maps a buffer to CPU-visible memory.
    /// </summary>
    IntPtr MapBuffer(IntPtr handle);

    /// <summary>
    /// Unmaps a buffer from CPU-visible memory.
    /// </summary>
    void UnmapBuffer(IntPtr handle);
}
