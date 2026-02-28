using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using System.Runtime.InteropServices;

namespace ArisenEngine.Rendering;

public enum IndexType
{
    Uint16 = (int)EFormat.FORMAT_R16_UINT,
    Uint32 = (int)EFormat.FORMAT_R32_UINT
}

public class IndexBuffer : IDisposable
{
    private RHIBufferHandle m_Handle;
    private uint m_Size;
    private uint m_Count;
    private IndexType m_IndexType;
    private string m_Name;

    public RHIBufferHandle Handle => m_Handle;
    public uint Size => m_Size;
    public uint Count => m_Count;
    public IndexType IndexType => m_IndexType;

    public IndexBuffer(uint count, IndexType indexType = IndexType.Uint32, string name = "IndexBuffer")
    {
        m_Count = count;
        m_IndexType = indexType;
        uint stride = (indexType == IndexType.Uint32) ? 4u : 2u;
        m_Size = count * stride;
        m_Name = name;
        
        var device = RHISystem.PrimaryDevice;
        if (!device.HasValue) throw new Exception("RHI Device not initialized");

        var factory = device.Value.GetFactory();
        
        m_Handle = factory.CreateBuffer(
            (ulong)m_Size, 
            (uint)EBufferUsageFlagBits.BUFFER_USAGE_INDEX_BUFFER_BIT, 
            ESharingMode.SHARING_MODE_EXCLUSIVE, 
            ERHIMemoryUsage.Upload, 
            m_Name);
    }

    public unsafe void SetData<T>(T[] data) where T : struct
    {
        int elementSize = Marshal.SizeOf<T>();
        int totalSize = elementSize * data.Length;
        if (totalSize > m_Size) throw new Exception("Data size exceeds buffer size");

        var device = RHISystem.PrimaryDevice;
        if (!device.HasValue) return;

        var factory = device.Value.GetFactory();
        
        void* ptr = factory.MapBuffer(m_Handle).ToPointer();
        
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

    public void Dispose()
    {
        if (m_Handle.IsValid)
        {
            var device = RHISystem.PrimaryDevice;
            if (device.HasValue)
            {
                device.Value.GetFactory().ReleaseBuffer(m_Handle);
            }
            m_Handle = RHIBufferHandle.Invalid;
        }
    }
}
