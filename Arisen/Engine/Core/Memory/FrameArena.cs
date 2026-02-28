using System.Runtime.InteropServices;

namespace ArisenEngine.Core.Memory;

/// <summary>
/// A high-performance linear allocator for data that only needs to live for a single frame.
/// </summary>
public unsafe sealed class FrameArena
{
    private byte* m_Buffer;
    private readonly nuint m_Capacity;
    private nuint m_Offset;

    public FrameArena(uint capacityInMB)
    {
        m_Capacity = (nuint)capacityInMB * 1024 * 1024;
        m_Buffer = (byte*)NativeMemory.Alloc(m_Capacity);
        m_Offset = 0;
    }

    /// <summary>
    /// Allocates unmanaged memory from the arena.
    /// </summary>
    public Span<T> Alloc<T>(int count) where T : unmanaged
    {
        nuint size = (nuint)count * (nuint)sizeof(T);
        
        // Alignment (assume 16-byte alignment for common data)
        nuint alignment = 16;
        nuint currentPtr = (nuint)m_Buffer + m_Offset;
        nuint alignedPtr = (currentPtr + (alignment - 1)) & ~(alignment - 1);
        nuint newOffset = alignedPtr - (nuint)m_Buffer + size;

        if (newOffset > m_Capacity)
            throw new OutOfMemoryException("FrameArena capacity exceeded!");

        m_Offset = newOffset;
        return new Span<T>((void*)alignedPtr, count);
    }

    /// <summary>
    /// Resets the arena offset to zero. Should be called at the end of each frame.
    /// </summary>
    public void Reset()
    {
        m_Offset = 0;
    }

    public void Dispose()
    {
        if (m_Buffer != null)
        {
            NativeMemory.Free(m_Buffer);
            m_Buffer = null;
        }
    }
}
