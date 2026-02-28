using System.Runtime.InteropServices;

namespace ArisenEngine.Core.Memory;

/// <summary>
/// A wrapper around unmanaged memory for high-performance data storage.
/// </summary>
public unsafe struct NativeArray<T> : IDisposable where T : unmanaged
{
    private void* m_Data;
    private readonly int m_Length;
    private bool m_IsDisposed;

    public int Length => m_Length;

    public NativeArray(int length)
    {
        m_Length = length;
        m_Data = NativeMemory.Alloc((nuint)length, (nuint)sizeof(T));
        m_IsDisposed = false;
    }

    public ref T this[int index]
    {
        get
        {
            if (index < 0 || index >= m_Length)
                throw new IndexOutOfRangeException();
            return ref ((T*)m_Data)[index];
        }
    }

    public Span<T> AsSpan()
    {
        if (m_IsDisposed) throw new ObjectDisposedException(nameof(NativeArray<T>));
        return new Span<T>(m_Data, m_Length);
    }

    public void Dispose()
    {
        if (!m_IsDisposed)
        {
            NativeMemory.Free(m_Data);
            m_Data = null;
            m_IsDisposed = true;
        }
    }
}
