using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Arisen.Native.RHI;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Native;
using ArisenEngine.Rendering;
using ArisenEngine.ECS.Lifecycle;
using ArisenKernel.Lifecycle;
using ArisenKernel.Diagnostics;
using ArisenKernel.Contracts;

namespace PackageGame;

public static class MeshRenderTest
{
    public static void Setup(SceneSubsystem scene)
    {
        KernelLog.Info("[MeshRenderTest] Setting up verification scene...");

        // 1. Register the MeshSystem
        scene.RegisterSystem(new MeshSystem());

        // 2. Create a test entity
        var em = scene.ActiveEntityManager;
        var entity = em.CreateEntity();

        // 3. Setup Transform (centered at origin, unit scale)
        em.AddComponent(entity, new TransformComponent
        {
            Position = new Vector3(0, 0, 5), // Move it in front of the camera
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });

        // 4. Setup MeshRenderer (passing raw RHI handles for DOD purity)
        // Resolve the IRHIDevice from the Service Registry and cast to its implementation
        var service = EngineKernel.Instance.Services.GetService<IRHIDevice>();
        
        if (service is VulkanRHIDevice vulkan)
        {
            // Reconstruct the RHI struct handle for the mesh factory
            var device = new RHIDevice(vulkan.NativeHandle);
            
            // Note: We still use the Mesh class here as a factory/storage for the test geometry.
            // But we extract the raw handles to store in the ECS Component.
            var mesh = new Mesh(device, "TestTriangle");
            
            var vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.0f,  0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0)
            };
            
            unsafe 
            {
                mesh.SetVertices(vertices, (uint)sizeof(Vector3));
            }
            
            var indices = new uint[] { 0, 1, 2 };
            mesh.SetIndices(indices);

            em.AddComponent(entity, new MeshRendererComponent
            {
                VertexBuffer = mesh.VertexBuffer.Handle,
                IndexBuffer = mesh.IndexBuffer.Handle,
                IndexCount = mesh.IndexBuffer.Count,
                // Correctly map the high-level IndexType to unmanaged EIndexType
                IndexType = mesh.IndexBuffer.IndexType == IndexType.Uint32 ? 
                            EIndexType.INDEX_TYPE_UINT32 : EIndexType.INDEX_TYPE_UINT16,
                MaterialID = 1
            });

            KernelLog.Info("[MeshRenderTest] Test entity created with raw RHI handles in MeshRendererComponent.");
        }
        else
        {
            KernelLog.Warning("[MeshRenderTest] No compatible VulkanRHIDevice service found, cannot create test mesh.");
        }
    }
}
