using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;

namespace ArisenEngine.Rendering;

public class Mesh : IDisposable
{
    private RHIDevice m_Device;
    private VertexBuffer m_VertexBuffer;
    private IndexBuffer m_IndexBuffer;
    private string m_Name;

    public VertexBuffer VertexBuffer => m_VertexBuffer;
    public IndexBuffer IndexBuffer => m_IndexBuffer;
    public string Name => m_Name;

    public Mesh(RHIDevice device, string name = "Mesh")
    {
        m_Device = device;
        m_Name = name;
    }

    public void SetVertices<T>(T[] vertices, uint stride) where T : struct
    {
        if (m_VertexBuffer == null || m_VertexBuffer.Count != (uint)vertices.Length || m_VertexBuffer.Stride != stride)
        {
            m_VertexBuffer?.Dispose();
            m_VertexBuffer = new VertexBuffer(m_Device, (uint)vertices.Length, stride, $"{m_Name}_VB");
        }

        m_VertexBuffer.SetData(vertices);
    }

    public void SetIndices(uint[] indices)
    {
        if (m_IndexBuffer == null || m_IndexBuffer.Count != (uint)indices.Length ||
            m_IndexBuffer.IndexType != IndexType.Uint32)
        {
            m_IndexBuffer?.Dispose();
            m_IndexBuffer = new IndexBuffer(m_Device, (uint)indices.Length, IndexType.Uint32, $"{m_Name}_IB");
        }

        m_IndexBuffer.SetData(indices);
    }

    public void SetIndices(ushort[] indices)
    {
        if (m_IndexBuffer == null || m_IndexBuffer.Count != (uint)indices.Length ||
            m_IndexBuffer.IndexType != IndexType.Uint16)
        {
            m_IndexBuffer?.Dispose();
            m_IndexBuffer = new IndexBuffer(m_Device, (uint)indices.Length, IndexType.Uint16, $"{m_Name}_IB");
        }

        m_IndexBuffer.SetData(indices);
    }

    public void Dispose()
    {
        m_VertexBuffer?.Dispose();
        m_IndexBuffer?.Dispose();
    }
}