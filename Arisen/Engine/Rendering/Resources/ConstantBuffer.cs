using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Rendering;

public class ConstantBuffer : IDisposable
{
    private RHIBufferHandle m_Handle;
    private uint m_Size;
    private string m_Name;

    public RHIBufferHandle Handle => m_Handle;
    public uint Size => m_Size;

    public ConstantBuffer(uint size, string name = "ConstantBuffer")
    {
        m_Size = size;
        m_Name = name;
        
        var device = RHISystem.Device;
        if (device == null) throw new Exception("RHI Device not initialized");

        var factory = device.GetFactory();
        
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

        var device = RHISystem.Device;
        if (device == null) return;

        var factory = device.GetFactory();
        
        void* ptr = factory.MapBuffer(m_Handle).ToPointer();
        Marshal.StructureToPtr(data, (IntPtr)ptr, false);
        factory.UnmapBuffer(m_Handle);
    }

    public void Dispose()
    {
        if (m_Handle.IsValid)
        {
            var device = RHISystem.Device;
            if (device != null)
            {
                device.GetFactory().ReleaseBuffer(m_Handle);
            }
            m_Handle = RHIBufferHandle.Invalid;
        }
    }
}
