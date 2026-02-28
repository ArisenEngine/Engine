using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Rendering;

public class ConstantBuffer : IDisposable
{
    private RHIDevice m_Device;
    private RHIBufferHandle m_Handle;
    private uint m_Size;
    private string m_Name;

    public RHIBufferHandle Handle => m_Handle;
    public uint Size => m_Size;

    private bool m_Disposed = false;

    public ConstantBuffer(RHIDevice device, uint size, string name = "ConstantBuffer")
    {
        m_Device = device;
        m_Size = size;
        m_Name = name;

        var factory = m_Device.GetFactory();

        m_Handle = factory.CreateBuffer(
            (ulong)m_Size,
            (uint)EBufferUsageFlagBits.BUFFER_USAGE_UNIFORM_BUFFER_BIT,
            ESharingMode.SHARING_MODE_EXCLUSIVE,
            ERHIMemoryUsage.Upload,
            m_Name);
    }

    public unsafe void UpdateData<T>(T data) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (size > m_Size) throw new Exception("Data size exceeds buffer size");

        var factory = m_Device.GetFactory();

        void* ptr = factory.MapBuffer(m_Handle).ToPointer();
        Marshal.StructureToPtr(data, (IntPtr)ptr, false);
        factory.UnmapBuffer(m_Handle);
    }

    ~ConstantBuffer()
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