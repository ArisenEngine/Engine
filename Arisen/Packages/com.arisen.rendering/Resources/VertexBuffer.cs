using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Rendering;

public class VertexBuffer : IDisposable
{
    private RHIDevice m_Device;
    private RHIBufferHandle m_Handle;
    private uint m_Size;
    private uint m_Count;
    private uint m_Stride;
    private string m_Name;

    public RHIBufferHandle Handle => m_Handle;
    public uint Size => m_Size;
    public uint Count => m_Count;
    public uint Stride => m_Stride;

    private bool m_Disposed = false;

    public VertexBuffer(RHIDevice device, uint count, uint stride, string name = "VertexBuffer")
    {
        m_Device = device;
        m_Count = count;
        m_Stride = stride;
        m_Size = count * stride;
        m_Name = name;

        var factory = m_Device.GetFactory();

        m_Handle = factory.CreateBuffer(
            (ulong)m_Size,
            (uint)EBufferUsageFlagBits.BUFFER_USAGE_VERTEX_BUFFER_BIT,
            ESharingMode.SHARING_MODE_EXCLUSIVE,
            ERHIMemoryUsage.Upload,
            m_Name);
    }

    public unsafe void SetData<T>(T[] data) where T : struct
    {
        int elementSize = Marshal.SizeOf<T>();
        int totalSize = elementSize * data.Length;
        if (totalSize > m_Size) throw new Exception("Data size exceeds buffer size");

        var factory = m_Device.GetFactory();

        void* ptr = factory.MapBuffer(m_Handle).ToPointer();

        // Manual copy for simplicity, can be optimized
        GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            NativeMemory.Copy(pin.AddrOfPinnedObject().ToPointer(), ptr, (nuint)totalSize);
        }
        finally
        {
            pin.Free();
        }

        factory.UnmapBuffer(m_Handle);
    }

    ~VertexBuffer()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (m_Disposed) return;

        if (m_Handle.IsValid)
        {
            m_Device.GetFactory().ReleaseBuffer(m_Handle);
            m_Handle = RHIBufferHandle.Invalid;
        }

        m_Disposed = true;
    }
}