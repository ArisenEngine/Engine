#pragma once

#include "../RHIRenderingTestBase.h"
#include "../../../Engine/NativeEngine/RHI/RayTracingExports.h"
#include "RHI/Resources/RHIAccelerationStructure.h"

namespace ArisenEngine::Testing
{
    class RHIRayTracingTest : public RHIRenderingTestBase
    {
    private:
        RHI_PSOHandle m_Pso = nullptr;
        RHI_PipelineHandle m_Pipeline = 0;
        
        RHI_AccelerationStructureHandle m_Blas = 0;
        RHI_AccelerationStructureHandle m_Tlas = 0;
        
        RHI_BufferHandle m_BlasBuffer = 0;
        RHI_BufferHandle m_TlasBuffer = 0;
        RHI_BufferHandle m_ScratchBuffer = 0;
        RHI_BufferHandle m_InstanceBuffer = 0;
        
        RHI_BufferHandle m_SbtBuffer = 0;
        
        RHI_ImageHandle m_StorageImage = 0;
        RHI_ImageViewHandle m_StorageImageView = 0;
        
        Containers::Vector<RHI_BufferHandle> m_CameraBuffers;
        
        struct CameraData
        {
            glm::mat4 viewInverse;
            glm::mat4 projInverse;
            glm::vec3 lightPos;
            float padding;
        };

    public:
        const char* GetName() const override { return "RayTracingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            if (!RHIRenderingTestBase::SetupTest()) return false;

            InitCommonResources();
            
            // Check RT support (implied by API existence, but in a real engine we'd check features)
            
            CreateResources();
            BuildAccelerationStructures();
            CreatePipeline();
            CreateSBT();

            return true;
        }

        void TeardownTest() override
        {
            for (auto& cb : m_CameraBuffers) RHI_Device_ReleaseBuffer(m_Device, cb);
            if (m_SbtBuffer) RHI_Device_ReleaseBuffer(m_Device, m_SbtBuffer);
            if (m_InstanceBuffer) RHI_Device_ReleaseBuffer(m_Device, m_InstanceBuffer);
            if (m_ScratchBuffer) RHI_Device_ReleaseBuffer(m_Device, m_ScratchBuffer);
            if (m_BlasBuffer) RHI_Device_ReleaseBuffer(m_Device, m_BlasBuffer);
            if (m_TlasBuffer) RHI_Device_ReleaseBuffer(m_Device, m_TlasBuffer);
            
            if (m_Blas) RHI_Device_ReleaseAccelerationStructure(m_Device, m_Blas);
            if (m_Tlas) RHI_Device_ReleaseAccelerationStructure(m_Device, m_Tlas);
            
            if (m_StorageImage) RHI_Device_ReleaseImage(m_Device, m_StorageImage);
            
            if (m_Pso) RHI_PSO_Release(m_Pso);
            
            m_Model.Release(m_Device);
            TeardownCommonResources();
            RHIRenderingTestBase::TeardownTest();
        }

    protected:
        void RenderFrame() override
        {
            auto currentIndex = GetCurrentFrameIndex();
            if (m_FrameTickets[currentIndex] > 0)
            {
                RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);
            }

            UpdateCameraData();
            RecordAndSubmit();

            NextFrame();
        }

    private:
        void CreateResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            
            std::filesystem::path modelPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "ABeautifulGame" / "glTF" / "ABeautifulGame.gltf";
            m_Model = LoadGLTF(modelPath.string());

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            // Storage Image for RT output
            RHI::RHIImageDescriptor imgDesc = {};
            imgDesc.imageType = RHI::IMAGE_TYPE_2D;
            imgDesc.width = width;
            imgDesc.height = height;
            imgDesc.depth = 1;
            imgDesc.mipLevels = 1;
            imgDesc.arrayLayers = 1;
            imgDesc.format = RHI::FORMAT_B8G8R8A8_UNORM;
            imgDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            imgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            imgDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
            imgDesc.usage = RHI::IMAGE_USAGE_STORAGE_BIT | RHI::IMAGE_USAGE_TRANSFER_SRC_BIT;
            imgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_StorageImage = RHI_Device_CreateImage(m_Device, &imgDesc, "RT Storage Image");

            RHI::RHIImageViewDesc viewDesc = {};
            viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            viewDesc.format = RHI::FORMAT_B8G8R8A8_UNORM;
            viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            viewDesc.levelCount = 1;
            viewDesc.layerCount = 1;
            m_StorageImageView = RHI_Image_AddImageView(m_Device, m_StorageImage, &viewDesc);

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor cbDesc = {};
                cbDesc.size = sizeof(CameraData);
                cbDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                cbDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_CameraBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &cbDesc, "Camera CB"));
            }

            m_CameraPos = glm::vec3(0.0f, 0.5f, 1.5f);
            m_CameraRot = glm::vec3(0.0f, 0.0f, 0.0f);
        }

        void BuildAccelerationStructures()
        {
            // 1. BLAS
            RHI::RHIAccelerationStructureGeometryData geom{};
            geom.type = RHI::ERHIAccelerationStructureGeometryType::Triangles;
            geom.flags = RHI::AS_GEOMETRY_OPAQUE_BIT;
            geom.triangles.vertexFormat = RHI::FORMAT_R32G32B32_SFLOAT;
            geom.triangles.vertexData = RHI_Buffer_GetDeviceAddress(m_Device, m_Model.vertexBuffer);
            geom.triangles.vertexStride = sizeof(GLTFVertex);
            geom.triangles.maxVertex = (UInt32)m_Model.vertexCount;
            geom.triangles.indexType = RHI::INDEX_TYPE_UINT32;
            geom.triangles.indexData = RHI_Buffer_GetDeviceAddress(m_Device, m_Model.indexBuffer);
            
            RHI::RHIAccelerationStructureBuildGeometryInfo blasInfo{};
            blasInfo.type = RHI::ERHIAccelerationStructureType::BottomLevel;
            blasInfo.flags = RHI::AS_BUILD_PREFER_FAST_TRACE_BIT;
            blasInfo.geometryCount = 1;
            blasInfo.pGeometries = &geom;

            UInt32 maxPrimCount = m_Model.indexCount / 3;
            RHI::RHIAccelerationStructureBuildSizesInfo blasSizes{};
            RHI_Device_GetAccelerationStructureBuildSizes(m_Device, &blasInfo, &maxPrimCount, &blasSizes);

            m_Blas = RHI_Device_CreateAccelerationStructure(m_Device, "BLAS");
            
            RHI::RHIBufferDescriptor blasBufDesc{};
            blasBufDesc.size = blasSizes.accelerationStructureSize;
            blasBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            blasBufDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_BlasBuffer = RHI_Device_CreateBuffer(m_Device, &blasBufDesc, "BLAS Buffer");
            
            RHI_Device_AllocAccelerationStructure(m_Device, m_Blas, (UInt32)blasInfo.type, blasSizes.accelerationStructureSize, m_BlasBuffer, 0);

            // 2. TLAS
            RHI::RHIAccelerationStructureInstance instance{};
            instance.transform = { 
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0
            };
            instance.instanceCustomIndex = 0;
            instance.mask = 0xFF;
            instance.instanceShaderBindingTableRecordOffset = 0;
            instance.flags = RHI::AS_INSTANCE_TRIANGLE_FACING_CULL_DISABLE_BIT;
            instance.accelerationStructureReference = RHI_Device_GetAccelerationStructureDeviceAddress(m_Device, m_Blas);

            RHI::RHIBufferDescriptor instBufDesc{};
            instBufDesc.size = sizeof(instance);
            instBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            instBufDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_InstanceBuffer = RHI_Device_CreateBuffer(m_Device, &instBufDesc, "Instance Buffer");
            RHI_Buffer_MemoryCopy(m_Device, m_InstanceBuffer, &instance, sizeof(instance), 0);

            RHI::RHIAccelerationStructureGeometryData tlasGeom{};
            tlasGeom.type = RHI::ERHIAccelerationStructureGeometryType::Instances;
            tlasGeom.instances.arrayOfPointers = false;
            tlasGeom.instances.data = RHI_Buffer_GetDeviceAddress(m_Device, m_InstanceBuffer);

            RHI::RHIAccelerationStructureBuildGeometryInfo tlasInfo{};
            tlasInfo.type = RHI::ERHIAccelerationStructureType::TopLevel;
            tlasInfo.flags = RHI::AS_BUILD_PREFER_FAST_TRACE_BIT;
            tlasInfo.geometryCount = 1;
            tlasInfo.pGeometries = &tlasGeom;

            UInt32 maxInstanceCount = 1;
            RHI::RHIAccelerationStructureBuildSizesInfo tlasSizes{};
            RHI_Device_GetAccelerationStructureBuildSizes(m_Device, &tlasInfo, &maxInstanceCount, &tlasSizes);

            m_Tlas = RHI_Device_CreateAccelerationStructure(m_Device, "TLAS");
            
            RHI::RHIBufferDescriptor tlasBufDesc{};
            tlasBufDesc.size = tlasSizes.accelerationStructureSize;
            tlasBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            tlasBufDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_TlasBuffer = RHI_Device_CreateBuffer(m_Device, &tlasBufDesc, "TLAS Buffer");
            
            if (!RHI_Device_AllocAccelerationStructure(m_Device, m_Tlas, (UInt32)tlasInfo.type, tlasSizes.accelerationStructureSize, m_TlasBuffer, 0))
            {
                LOG_ERROR("Failed to Alloc TLAS! (RHI_Device_AllocAccelerationStructure returned false)");
            }
            else
            {
                LOG_INFO("Alloc TLAS Success.");
            }
            UInt64 tlasAddr = RHI_Device_GetAccelerationStructureDeviceAddress(m_Device, m_Tlas);
            LOG_INFO("TLAS Device Address: " + std::to_string(tlasAddr));


            // 3. Scratch Buffer
            RHI::RHIBufferDescriptor scratchDesc{};
            scratchDesc.size = (std::max)(blasSizes.buildScratchSize, tlasSizes.buildScratchSize);
            scratchDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            scratchDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_ScratchBuffer = RHI_Device_CreateBuffer(m_Device, &scratchDesc, "AS Scratch Buffer");

            // 4. Build Commands
            auto cmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, cmdPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            
            blasInfo.dstAccelerationStructure = *reinterpret_cast<RHI::RHIAccelerationStructureHandle*>(&m_Blas);
            blasInfo.scratchData = *reinterpret_cast<RHI::RHIBufferHandle*>(&m_ScratchBuffer);
            
            RHI::RHIAccelerationStructureBuildRangeInfo blasRange{};
            blasRange.primitiveCount = maxPrimCount;
            blasRange.primitiveOffset = 0;
            blasRange.firstVertex = 0;
            blasRange.transformOffset = 0;
            const RHI::RHIAccelerationStructureBuildRangeInfo* pBlasRange = &blasRange;
            RHI_Cmd_BuildAccelerationStructures(cmd, 1, &blasInfo, &pBlasRange);

            // Barrier between BLAS and TLAS
            RHI::RHIMemoryBarrier barrier{};
            barrier.srcAccessMask = RHI::ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR;
            barrier.dstAccessMask = RHI::ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
            RHI_Cmd_PipelineBarrier_Memory(cmd, RHI::PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, RHI::PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, 0, 1, &barrier);

            tlasInfo.dstAccelerationStructure = *reinterpret_cast<RHI::RHIAccelerationStructureHandle*>(&m_Tlas);
            tlasInfo.scratchData = *reinterpret_cast<RHI::RHIBufferHandle*>(&m_ScratchBuffer);
            
            RHI::RHIAccelerationStructureBuildRangeInfo tlasRange{};
            tlasRange.primitiveCount = 1;
            tlasRange.primitiveOffset = 0;
            tlasRange.firstVertex = 0;
            tlasRange.transformOffset = 0;
            const RHI::RHIAccelerationStructureBuildRangeInfo* pTlasRange = &tlasRange;
            RHI_Cmd_BuildAccelerationStructures(cmd, 1, &tlasInfo, &pTlasRange);

            RHI_Cmd_End(cmd);
            RHI_Device_Submit(m_Device, cmd, 0);
            RHI_Device_WaitIdle(m_Device);
            
            RHI_Device_ReleaseCommandBuffer(m_Device, cmdPool, 0, cmd);
            RHI_Device_ReleaseCommandBufferPool(m_Device, cmdPool);
        }

        void CreatePipeline()
        {
            auto pm = RHI_Device_GetPipelineManager(m_Device);
            m_Pso = RHI_PipelineManager_CreatePSO(pm);
            RHI_PSO_SetBindPoint(m_Pso, RHI::PIPELINE_BIND_POINT_RAY_TRACING_KHR);

            // Use specific profiles to ensure proper reflection of stage flags
            auto rgen = CompileShader(L"RayTracingTest", "RayGen", "6_3");
            auto rmiss = CompileShader(L"RayTracingTest", "Miss", "6_3");
            auto rchit = CompileShader(L"RayTracingTest", "ClosestHit", "6_3");

            RHI_PSO_AddProgram(m_Pso, rgen);
            RHI_PSO_AddProgram(m_Pso, rmiss);
            RHI_PSO_AddProgram(m_Pso, rchit);

            // Groups
            RHI::RHIRayTracingShaderGroup rgenGroup{};
            rgenGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            rgenGroup.generalShaderIndex = 0;
            RHI_PSO_AddRayTracingShaderGroup(m_Pso, &rgenGroup);

            RHI::RHIRayTracingShaderGroup missGroup{};
            missGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            missGroup.generalShaderIndex = 1;
            RHI_PSO_AddRayTracingShaderGroup(m_Pso, &missGroup);

            RHI::RHIRayTracingShaderGroup hitGroup{};
            hitGroup.type = RHI::ERHIRayTracingShaderGroupType::TrianglesHitGroup;
            hitGroup.closestHitShaderIndex = 2;
            RHI_PSO_AddRayTracingShaderGroup(m_Pso, &hitGroup);

            RHI_PSO_SetMaxRecursionDepth(m_Pso, 1);

            // Descriptors
            if (!m_Tlas)
            {
                LOG_ERROR("TLAS handle is invalid (0) in CreatePipeline!");
            }

            Containers::Vector<RHI_AccelerationStructureHandle> tlasses = { m_Tlas };
            RHI_PSO_UpdateDescriptorSet_AccelerationStructures(m_Pso, 0, 0, &tlasses);
            
            Containers::Vector<RHI::RHIDescriptorImageInfo> images;
            RHI::RHIDescriptorImageInfo storageInfo{};
            storageInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_StorageImageView);
            storageInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
            images.push_back(storageInfo);
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &images);
            
            Containers::Vector<RHI_BufferHandle> ubos = { m_CameraBuffers[0] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 2, &ubos);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            // Initialize descriptor pools for each frame in flight
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = {
                    RHI::DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                    RHI::DESCRIPTOR_TYPE_STORAGE_IMAGE,
                    RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER
                };
                Containers::Vector<UInt32> counts = { 1, 1, 1 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
            }

            m_Pipeline = RHI_PipelineManager_GetRayTracingPipeline(pm, m_Pso);
        }


        void CreateSBT()
        {
            // SBT must be aligned to shaderGroupBaseAlignment (usually 64 bytes)
            UInt32 handleSize = 32;          // Size of the shader group handle (device property, usually 32)
            UInt32 groupStride = 64;         // Stride must be aligned to shaderGroupBaseAlignment
            UInt32 sbtSize = groupStride * 3;

            RHI::RHIBufferDescriptor sbtDesc{};
            sbtDesc.size = sbtSize;
            sbtDesc.usage = RHI::BUFFER_USAGE_SHADER_BINDING_TABLE_BIT_KHR | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            sbtDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_SbtBuffer = RHI_Device_CreateBuffer(m_Device, &sbtDesc, "SBT Buffer");

            uint8_t* pSbtData = (uint8_t*)RHI_Buffer_Map(m_Device, m_SbtBuffer);
            
            // Get handles in a temp buffer
            std::vector<uint8_t> tempHandles(handleSize * 3);
            RHI_Device_GetRayTracingShaderGroupHandles(m_Device, m_Pipeline, 0, 3, tempHandles.size(), tempHandles.data());
            
            // Write to SBT with alignment padding
            std::memset(pSbtData, 0, sbtSize);
            std::memcpy(pSbtData + 0 * groupStride, tempHandles.data() + 0 * handleSize, handleSize);
            std::memcpy(pSbtData + 1 * groupStride, tempHandles.data() + 1 * handleSize, handleSize);
            std::memcpy(pSbtData + 2 * groupStride, tempHandles.data() + 2 * handleSize, handleSize);

            RHI_Buffer_Unmap(m_Device, m_SbtBuffer);
        }

        void UpdateCameraData()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);

            CameraData data;
            data.viewInverse = glm::inverse(GetViewMatrix());
            data.projInverse = glm::inverse(GetProjectionMatrix(width / height));
            data.lightPos = glm::vec3(2.0f, 5.0f, 2.0f);
            
            RHI_Buffer_MemoryCopy(m_Device, m_CameraBuffers[GetCurrentFrameIndex()], &data, sizeof(CameraData), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);
            
            RHI_Cmd_Begin(cmd, currentIndex, 0);

            // Update descriptors for current frame
            Containers::Vector<RHI_BufferHandle> ubos = { m_CameraBuffers[currentIndex] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 2, &ubos);
            
            // Re-allocate or re-update descriptor set if necessary, 
            // but for RT let's assume we can reuse/rebake
            UInt32 poolId = m_DescriptorPoolIds[currentIndex];
            RHI_DescriptorPool_Reset(m_DescriptorPool, poolId);
            UInt32 setIdx = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, poolId, 0, m_Pso);
            RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, poolId, setIdx, m_Pso);

            RHI_Cmd_BindPipeline(cmd, m_Pipeline);
            RHI_Cmd_BindDescriptorSet_FromPool(cmd, RHI::PIPELINE_BIND_POINT_RAY_TRACING_KHR, 0, m_DescriptorPool, poolId, setIdx);

            if (m_Tlas == 0)
            {
               LOG_ERROR("TLAS handle is 0 in RecordAndSubmit!");
            }


            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            // Barrier: Transition Storage Image to GENERAL
            RHI_Cmd_TransitionImageLayout(cmd, m_StorageImage, RHI::IMAGE_LAYOUT_GENERAL);

            RHI::RHITraceRaysDescriptor traceDesc{};
            UInt64 sbtAddr = RHI_Buffer_GetDeviceAddress(m_Device, m_SbtBuffer);
            UInt32 groupStride = 64;

            traceDesc.raygenShaderRecord = { sbtAddr + 0 * groupStride, groupStride, groupStride };
            traceDesc.missShaderTable = { sbtAddr + 1 * groupStride, groupStride, groupStride };
            traceDesc.hitShaderTable = { sbtAddr + 2 * groupStride, groupStride, groupStride };
            traceDesc.width = width;
            traceDesc.height = height;
            traceDesc.depth = 1;

            RHI_Cmd_TraceRays(cmd, &traceDesc);

            // Copy storage image to swapchain
            auto colorBuffer = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
            if (colorBuffer)
            {
                // Barrier: Transition Storage Image to TRANSFER_SRC
                RHI_Cmd_TransitionImageLayout(cmd, m_StorageImage, RHI::IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL);
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL);
                
                RHI::RHIImageCopy region{};
                region.srcSubresource = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 };
                region.srcOffset = { 0, 0, 0 };
                region.dstSubresource = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 };
                region.dstOffset = { 0, 0, 0 };
                region.extent = { width, height, 1 };
                
                Containers::Vector<RHI::RHIImageCopy> regions = { region };
                RHI_Cmd_CopyImage(cmd, m_StorageImage, RHI::IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, colorBuffer, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, (UInt32)regions.size(), regions.data());
                
                RHI_Cmd_TransitionImageLayout(cmd, colorBuffer, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            }

            RHI_Cmd_End(cmd);

            RHI::RHISubmitDescriptor submitDesc = {};
            submitDesc.WaitSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            submitDesc.SignalSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
            m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));
            
            RHI_SwapChain_EndFrame(m_SwapChain, currentIndex);
            RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
        }

        RHI_GPUProgramHandle CompileShader(const String& name, const String& entry, const String& profile)
        {
            String envStr = GetShaderEnvString();
            
            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            String currentPath = exeDir.generic_wstring().c_str();
            currentPath += "\\Shader";
            String path = currentPath + "\\" + name + ".hlsl";

            HAL::ShaderCompileParams params{};
            params.input = path;
            params.entry = entry;
            params.shaderModel = profile; // Utilizes the specific profile passed (e.g. "raygeneration_6_3")
            params.target = L"-spirv";
            params.targetEnv = L"vulkan1.2";
            params.optimizeLevel = L"0";
            params.stage = RHI::EProgramStage::RayTracing; // Raytracing shaders are usually in a library

            HAL::ShaderCompilerOutput output;
            if (!HAL::CompileShaderFromFile(std::move(params), output) || output.codePointer == nullptr || output.codeSize == 0)
            {
                LOG_ERRORF("Failed to compile shader: {0} {1}", name.c_str(), entry.c_str());
                return 0;
            }

            RHI_GPUProgramHandle prog = RHI_Device_CreateGPUProgram(m_Device);
            RHI::RHIShaderProgramDesc desc = { (UInt32)output.codeSize, output.codePointer, entry.c_str(), name.c_str(), RHI::SHADER_STAGE_RAYGEN_BIT };
            
            // Map entry to stage bits (simplified for test)
            if (entry.Contains("RayGen")) desc.stage = RHI::SHADER_STAGE_RAYGEN_BIT;
            else if (entry.Contains("Miss")) desc.stage = RHI::SHADER_STAGE_MISS_BIT;
            else if (entry.Contains("ClosestHit")) desc.stage = RHI::SHADER_STAGE_CLOSEST_HIT_BIT;

            RHI_Device_AttachProgramByteCode(m_Device, prog, &desc);
            
            if (output.codePointer) std::free(output.codePointer);
            
            return prog;
        }
    };
}
