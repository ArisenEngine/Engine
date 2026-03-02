#pragma once

#include "../RHIRenderingTestBase.h"
#include "RHI/Resources/RHIAccelerationStructure.h"
#include "Logger/Logger.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Presentation/RHISwapChain.h"

#include <memory>
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"

namespace ArisenEngine::Testing
{
    class RHIRayTracingTest : public RHIRenderingTestBase
    {
    private:
        std::unique_ptr<RHI::RHIPipelineState> m_Pso;
        RHI::RHIPipelineHandle m_Pipeline;

        RHI::RHIAccelerationStructureHandle m_Blas;
        RHI::RHIAccelerationStructureHandle m_Tlas;

        RHI::RHIBufferHandle m_BlasBuffer;
        RHI::RHIBufferHandle m_TlasBuffer;
        RHI::RHIBufferHandle m_ScratchBuffer;
        RHI::RHIBufferHandle m_InstanceBuffer;

        RHI::RHIBufferHandle m_SbtBuffer;

        RHI::RHIImageHandle m_StorageImage;
        RHI::RHIImageViewHandle m_StorageImageView;

        RHI::RHIBufferHandle m_MaterialBuffer;
        RHI::RHIBufferHandle m_TriangleMaterialBuffer;
        Containers::Vector<RHI::RHIImageViewHandle> m_ModelTextures;
        RHI::RHISamplerHandle m_DefaultSampler;

        Containers::Vector<RHI::RHIBufferHandle> m_CameraBuffers;
        Containers::Vector<UInt32> m_DescriptorSetIndices;

        struct PointLight
        {
            glm::vec4 posRange; // xyz: pos, w: range
            glm::vec4 colorInt; // xyz: color, w: intensity
        };

        struct CameraData
        {
            glm::mat4 viewInverse;
            glm::mat4 projInverse;
            glm::vec4 cameraPos; // xyz: pos, w: unused
            glm::vec4 lightPosAndFrameCount; // xyz: sunPos, w: frameCount
            PointLight pointLights[8];
            int numPointLights;
            int padding[3];
        };

        RHI::RHIImageHandle m_AccumulationImage;
        RHI::RHIImageViewHandle m_AccumulationImageView;
        UInt32 m_AccumulatedFrames = 0;

        glm::vec3 m_PrevCameraPos = glm::vec3(0.0f);
        glm::vec3 m_PrevCameraRot = glm::vec3(0.0f);

        struct MaterialData
        {
            glm::vec4 baseColorFactor;
            int baseColorTextureIndex;
            float metallicFactor;
            float roughnessFactor;
            int padding;
        };

        struct SubmeshData
        {
            UInt32 materialIndex;
            UInt32 firstIndex;
            UInt32 padding[2];
        };

    public:
        const char* GetName() const override { return "RayTracingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }

        bool SetupTest() override
        {
            if (!RHIRenderingTestBase::SetupTest()) return false;

            auto caps = m_Device->GetCapabilities();
            if (!caps.rayTracingSupported)
            {
                LOG_WARN("Ray Tracing extension not supported or enabled, skipping test.");
                return false;
            }

            InitCommonResources();

            CreateCommonResources();
            BuildAccelerationStructures();
            CreateSizeDependentResources();
            CreatePipeline();
            CreateSBT();

            return true;
        }

        void TeardownTest() override
        {
            auto factory = m_Device->GetFactory();
            for (auto& cb : m_CameraBuffers) if (cb.IsValid()) factory->ReleaseBuffer(cb);
            if (m_SbtBuffer.IsValid()) factory->ReleaseBuffer(m_SbtBuffer);
            if (m_InstanceBuffer.IsValid()) factory->ReleaseBuffer(m_InstanceBuffer);
            if (m_ScratchBuffer.IsValid()) factory->ReleaseBuffer(m_ScratchBuffer);
            if (m_BlasBuffer.IsValid()) factory->ReleaseBuffer(m_BlasBuffer);
            if (m_TlasBuffer.IsValid()) factory->ReleaseBuffer(m_TlasBuffer);

            if (m_Blas.IsValid()) factory->ReleaseAccelerationStructure(m_Blas);
            if (m_Tlas.IsValid()) factory->ReleaseAccelerationStructure(m_Tlas);

            if (m_StorageImageView.IsValid()) factory->ReleaseImageView(m_StorageImageView);
            if (m_StorageImage.IsValid()) factory->ReleaseImage(m_StorageImage);
            if (m_AccumulationImageView.IsValid()) factory->ReleaseImageView(m_AccumulationImageView);
            if (m_AccumulationImage.IsValid()) factory->ReleaseImage(m_AccumulationImage);

            m_Pso.reset();

            if (m_MaterialBuffer.IsValid()) factory->ReleaseBuffer(m_MaterialBuffer);
            if (m_TriangleMaterialBuffer.IsValid()) factory->ReleaseBuffer(m_TriangleMaterialBuffer);
            if (m_DefaultSampler.IsValid()) factory->ReleaseSampler(m_DefaultSampler);

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
                m_Device->GetQueue(RHI::RHIQueueType::Graphics)->WaitForTicket(m_FrameTickets[currentIndex]);
            }

            UpdateCameraData();
            RecordAndSubmit();

            NextFrame();
        }

        void OnResize(UInt32 width, UInt32 height) override
        {
            if (width == 0 || height == 0) return;

            m_AccumulatedFrames = 0;
            auto factory = m_Device->GetFactory();
            if (m_StorageImageView.IsValid()) factory->ReleaseImageView(m_StorageImageView);
            if (m_StorageImage.IsValid()) factory->ReleaseImage(m_StorageImage);
            if (m_AccumulationImageView.IsValid()) factory->ReleaseImageView(m_AccumulationImageView);
            if (m_AccumulationImage.IsValid()) factory->ReleaseImage(m_AccumulationImage);

            m_StorageImageView = {};
            m_StorageImage = {};
            m_AccumulationImageView = {};
            m_AccumulationImage = {};

            CreateSizeDependentResources();

            // Update descriptors for images in PSO
            if (m_Pso)
            {
                RHI::RHIDescriptorImageInfo storageInfo{};
                storageInfo.imageView = m_StorageImageView;
                storageInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
                m_Pso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIDescriptorImageInfo>{storageInfo});

                RHI::RHIDescriptorImageInfo accumInfo{};
                accumInfo.imageView = m_AccumulationImageView;
                accumInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
                m_Pso->UpdateDescriptorSet(0, 9, Containers::Vector<RHI::RHIDescriptorImageInfo>{accumInfo});
            }
        }

    private:
        void CreateCommonResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();

            std::filesystem::path modelPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" /
                "Sponza.gltf";
            m_Model = LoadGLTF(modelPath.string());

            auto factory = m_Device->GetFactory();
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor cbDesc = {};
                cbDesc.size = sizeof(CameraData);
                cbDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                cbDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
                m_CameraBuffers.push_back(factory->CreateBuffer(std::move(cbDesc), "Camera CB"));
            }

            // Material and Primitive Data
            m_ModelTextures.clear();
            Containers::Vector<MaterialData> matData;
            for (auto& mat : m_Model.materials)
            {
                MaterialData md{};
                md.baseColorFactor = mat.baseColorFactor;

                if (mat.baseColorView.IsValid() && m_ModelTextures.size() < 100)
                {
                    md.baseColorTextureIndex = (int)m_ModelTextures.size();
                    m_ModelTextures.push_back(mat.baseColorView);
                }
                else
                {
                    md.baseColorTextureIndex = -1;
                }

                md.metallicFactor = 0.0f;
                md.roughnessFactor = 1.0f;
                matData.push_back(md);
            }

            RHI::RHIBufferDescriptor matBufDesc{};
            matBufDesc.size = matData.size() * sizeof(MaterialData);
            matBufDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT |
                RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            matBufDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_MaterialBuffer = factory->CreateBuffer(std::move(matBufDesc), "Material Buffer");

            // Create staging buffer for upload
            RHI::RHIBufferDescriptor stagingDesc{};
            stagingDesc.size = matData.size() * sizeof(MaterialData);
            stagingDesc.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
            stagingDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            auto stagingBuffer = factory->CreateBuffer(std::move(stagingDesc), "Material Staging Buffer");
            m_Device->GetFactory()->BufferMemoryCopy(stagingBuffer, matData.data(),
                                                     matData.size() * sizeof(MaterialData), 0);

            // Copy from staging to device local buffer
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            cmd->Begin(0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            cmd->CopyBuffer(stagingBuffer, 0, m_MaterialBuffer, 0, matData.size() * sizeof(MaterialData));
            cmd->End();
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle);
            m_Device->DeviceWaitIdle();
            pool->ReleaseCommandBuffer(0, cmdHandle);
            factory->ReleaseBuffer(stagingBuffer);

            // Per-Submesh Data Buffer
            Containers::Vector<SubmeshData> submeshData;
            for (size_t i = 0; i < m_Model.primitives.size(); ++i)
            {
                const auto& prim = m_Model.primitives[i];
                submeshData.push_back({(UInt32)prim.materialIndex, prim.firstIndex, {0, 0}});
            }

            RHI::RHIBufferDescriptor triBufDesc{};
            triBufDesc.size = submeshData.size() * sizeof(SubmeshData);
            triBufDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            triBufDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_TriangleMaterialBuffer = factory->CreateBuffer(std::move(triBufDesc), "Submesh Data Buffer");
            m_Device->GetFactory()->BufferMemoryCopy(m_TriangleMaterialBuffer, submeshData.data(),
                                                     submeshData.size() * sizeof(SubmeshData), 0);

            m_CameraPos = glm::vec3(0.0f, 5.0f, 10.0f);
            m_CameraRot = glm::vec3(0.0f, -glm::half_pi<float>(), 0.0f);
            m_PrevCameraPos = m_CameraPos;
            m_PrevCameraRot = m_CameraRot;

            // Default Sampler
            RHI::RHISamplerDesc sampDesc = {};
            sampDesc.magFilter = RHI::FILTER_LINEAR;
            sampDesc.minFilter = RHI::FILTER_LINEAR;
            sampDesc.mipmapMode = RHI::SAMPLER_MIPMAP_MODE_LINEAR;
            sampDesc.maxLod = 16.0f;
            sampDesc.addressModeU = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            sampDesc.addressModeV = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            sampDesc.addressModeW = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            m_DefaultSampler = factory->CreateSampler(std::move(sampDesc));
        }

        void CreateSizeDependentResources()
        {
            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            if (width == 0 || height == 0)
            {
                width = 1280;
                height = 720;
            }

            auto factory = m_Device->GetFactory();
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
            imgDesc.usage = RHI::IMAGE_USAGE_STORAGE_BIT | RHI::IMAGE_USAGE_TRANSFER_SRC_BIT |
                RHI::IMAGE_USAGE_TRANSFER_DST_BIT;
            imgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            imgDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_StorageImage = factory->CreateImage(std::move(imgDesc), "RT Storage Image");

            RHI::RHIImageViewDesc viewDesc = {};
            viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            viewDesc.format = RHI::FORMAT_B8G8R8A8_UNORM;
            viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            viewDesc.levelCount = 1;
            viewDesc.layerCount = 1;
            viewDesc.width = width;
            viewDesc.height = height;
            m_StorageImageView = factory->CreateImageView(m_StorageImage, std::move(viewDesc));

            // Accumulation Image (32-bit float for high precision)
            RHI::RHIImageDescriptor accDesc = {};
            accDesc.imageType = RHI::IMAGE_TYPE_2D;
            accDesc.width = width;
            accDesc.height = height;
            accDesc.depth = 1;
            accDesc.mipLevels = 1;
            accDesc.arrayLayers = 1;
            accDesc.format = RHI::FORMAT_R32G32B32A32_SFLOAT;
            accDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            accDesc.usage = RHI::IMAGE_USAGE_STORAGE_BIT | RHI::IMAGE_USAGE_TRANSFER_SRC_BIT |
                RHI::IMAGE_USAGE_TRANSFER_DST_BIT;
            accDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            accDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_AccumulationImage = factory->CreateImage(std::move(accDesc), "RT Accumulation Image");

            RHI::RHIImageViewDesc accViewDesc = {};
            accViewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            accViewDesc.format = RHI::FORMAT_R32G32B32A32_SFLOAT;
            accViewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            accViewDesc.levelCount = 1;
            accViewDesc.layerCount = 1;
            accViewDesc.width = width;
            accViewDesc.height = height;
            m_AccumulationImageView = factory->CreateImageView(m_AccumulationImage, std::move(accViewDesc));
        }

        void BuildAccelerationStructures()
        {
            auto factory = m_Device->GetFactory();
            RHI::RHIAccelerationStructureBuildSizesInfo blasSizes{};
            // 1. BLAS
            Containers::Vector<RHI::RHIAccelerationStructureGeometryData> geometries;
            Containers::Vector<UInt32> maxPrimCounts;
            Containers::Vector<RHI::RHIAccelerationStructureBuildRangeInfo> buildRanges;

            for (const auto& prim : m_Model.primitives)
            {
                RHI::RHIAccelerationStructureGeometryData geom{};
                geom.type = RHI::ERHIAccelerationStructureGeometryType::Triangles;
                geom.flags = RHI::AS_GEOMETRY_OPAQUE_BIT;
                geom.triangles.vertexFormat = RHI::FORMAT_R32G32B32_SFLOAT;
                geom.triangles.vertexData = m_Device->GetFactory()->GetBufferDeviceAddress(m_Model.vertexBuffer);
                geom.triangles.vertexStride = sizeof(GLTFVertex);
                geom.triangles.maxVertex = (UInt32)m_Model.vertexCount;
                geom.triangles.indexType = RHI::INDEX_TYPE_UINT32;
                geom.triangles.indexData = m_Device->GetFactory()->GetBufferDeviceAddress(m_Model.indexBuffer);
                geometries.push_back(geom);

                maxPrimCounts.push_back(prim.indexCount / 3);

                RHI::RHIAccelerationStructureBuildRangeInfo range{};
                range.primitiveCount = prim.indexCount / 3;
                range.primitiveOffset = prim.firstIndex * sizeof(UInt32);
                range.firstVertex = 0;
                range.transformOffset = 0;
                buildRanges.push_back(range);
            }

            RHI::RHIAccelerationStructureBuildGeometryInfo blasInfo{};
            blasInfo.type = RHI::ERHIAccelerationStructureType::BottomLevel;
            blasInfo.flags = RHI::AS_BUILD_PREFER_FAST_TRACE_BIT;
            blasInfo.geometryCount = (UInt32)geometries.size();
            blasInfo.pGeometries = geometries.data();

            blasSizes.accelerationStructureSize = 0; // Initialize
            m_Device->GetRayTracing()->GetAccelerationStructureBuildSizes(blasInfo, maxPrimCounts.data(), &blasSizes);

            m_Blas = factory->CreateAccelerationStructure("BLAS");

            RHI::RHIBufferDescriptor blasBufDesc{};
            blasBufDesc.size = (UInt32)blasSizes.accelerationStructureSize;
            blasBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR |
                RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            blasBufDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_BlasBuffer = factory->CreateBuffer(std::move(blasBufDesc), "BLAS Buffer");

            auto* backend = dynamic_cast<RHI::IRHIBackend*>(m_Device);
            if (!backend || !backend->AllocAccelerationStructure(m_Blas, blasInfo.type,
                                                                 blasSizes.accelerationStructureSize, m_BlasBuffer, 0))
            {
                LOG_ERROR("Failed to allocate BLAS!");
                return;
            }

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
            instance.accelerationStructureReference = m_Device->GetRayTracing()->
                                                                GetAccelerationStructureDeviceAddress(m_Blas);

            RHI::RHIBufferDescriptor instBufDesc{};
            instBufDesc.size = sizeof(instance);
            instBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR |
                RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            instBufDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_InstanceBuffer = factory->CreateBuffer(std::move(instBufDesc), "Instance Buffer");
            m_Device->GetFactory()->BufferMemoryCopy(m_InstanceBuffer, &instance, sizeof(instance), 0);

            RHI::RHIAccelerationStructureGeometryData tlasGeom{};
            tlasGeom.type = RHI::ERHIAccelerationStructureGeometryType::Instances;
            tlasGeom.instances.arrayOfPointers = false;
            tlasGeom.instances.data = m_Device->GetFactory()->GetBufferDeviceAddress(m_InstanceBuffer);

            RHI::RHIAccelerationStructureBuildGeometryInfo tlasInfo{};
            tlasInfo.type = RHI::ERHIAccelerationStructureType::TopLevel;
            tlasInfo.flags = RHI::AS_BUILD_PREFER_FAST_TRACE_BIT;
            tlasInfo.geometryCount = 1;
            tlasInfo.pGeometries = &tlasGeom;

            UInt32 maxInstanceCount = 1;
            RHI::RHIAccelerationStructureBuildSizesInfo tlasSizes{};
            m_Device->GetRayTracing()->GetAccelerationStructureBuildSizes(tlasInfo, &maxInstanceCount, &tlasSizes);

            RHI::RHIBufferDescriptor tlasBufDesc{};
            tlasBufDesc.size = (UInt32)tlasSizes.accelerationStructureSize;
            tlasBufDesc.usage = RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_STORAGE_BIT_KHR |
                RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            tlasBufDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_TlasBuffer = factory->CreateBuffer(std::move(tlasBufDesc), "TLAS Buffer");
            m_Tlas = factory->CreateAccelerationStructure("TLAS");

            auto* backendTlas = dynamic_cast<RHI::IRHIBackend*>(m_Device);
            if (!backendTlas)
            {
                LOG_ERROR("Device is not a valid RHI backend!");
                return;
            }

            if (!backendTlas->AllocAccelerationStructure(m_Tlas, RHI::ERHIAccelerationStructureType::TopLevel,
                                                         tlasSizes.accelerationStructureSize, m_TlasBuffer, 0))
            {
                LOG_ERROR("Failed to allocate TLAS!");
                return;
            }

            // 3. Scratch Buffer
            RHI::RHIBufferDescriptor scratchDesc{};
            scratchDesc.size = (std::max<UInt64>)(blasSizes.buildScratchSize, tlasSizes.buildScratchSize);
            scratchDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            scratchDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
            m_ScratchBuffer = factory->CreateBuffer(std::move(scratchDesc), "AS Scratch Buffer");

            // 4. Build Commands
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);
            cmd->Begin(0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            blasInfo.dstAccelerationStructure = m_Blas;
            blasInfo.scratchData = m_ScratchBuffer;

            const RHI::RHIAccelerationStructureBuildRangeInfo* pBlasRanges = buildRanges.data();
            cmd->BuildAccelerationStructures(1, &blasInfo, &pBlasRanges);

            // Barrier between BLAS and TLAS
            RHI::RHIMemoryBarrier barrier{};
            barrier.srcAccessMask = RHI::ACCESS_ACCELERATION_STRUCTURE_WRITE_BIT_KHR;
            barrier.dstAccessMask = RHI::ACCESS_ACCELERATION_STRUCTURE_READ_BIT_KHR;
            cmd->PipelineBarrier(RHI::PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR,
                                 RHI::PIPELINE_STAGE_ACCELERATION_STRUCTURE_BUILD_BIT_KHR, 0, {barrier}, {}, {});

            tlasInfo.dstAccelerationStructure = m_Tlas;
            tlasInfo.scratchData = m_ScratchBuffer;

            RHI::RHIAccelerationStructureBuildRangeInfo tlasRange{};
            tlasRange.primitiveCount = 1;
            tlasRange.primitiveOffset = 0;
            tlasRange.firstVertex = 0;
            tlasRange.transformOffset = 0;
            const RHI::RHIAccelerationStructureBuildRangeInfo* pTlasRange = &tlasRange;
            cmd->BuildAccelerationStructures(1, &tlasInfo, &pTlasRange);

            cmd->End();
            RHI::RHISubmitDescriptor submitDesc = {};
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle, &submitDesc);
            m_Device->DeviceWaitIdle();
            pool->ReleaseCommandBuffer(0, cmdHandle);
        }

        void CreatePipeline()
        {
            auto pm = m_Device->GetPipelineCache();
            m_Pso = pm->GetPipelineState();
            m_Pso->SetBindPoint(RHI::PIPELINE_BIND_POINT_RAY_TRACING_KHR);

            auto rgen = CompileShader(L"RayTracingTest", "RayGen", "6_3");
            auto rmiss = CompileShader(L"RayTracingTest", "Miss", "6_3");
            auto rchit = CompileShader(L"RayTracingTest", "ClosestHit", "6_3");
            auto smiss = CompileShader(L"RayTracingTest", "ShadowMiss", "6_3");

            m_Pso->AddProgram(rgen);
            m_Pso->AddProgram(rmiss);
            m_Pso->AddProgram(rchit);
            m_Pso->AddProgram(smiss);

            // Groups
            RHI::RHIRayTracingShaderGroup rgenGroup{};
            rgenGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            rgenGroup.generalShaderIndex = 0;
            m_Pso->AddRayTracingShaderGroup(rgenGroup);

            RHI::RHIRayTracingShaderGroup missGroup{};
            missGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            missGroup.generalShaderIndex = 1;
            m_Pso->AddRayTracingShaderGroup(missGroup);

            RHI::RHIRayTracingShaderGroup hitGroup{};
            hitGroup.type = RHI::ERHIRayTracingShaderGroupType::TrianglesHitGroup;
            hitGroup.closestHitShaderIndex = 2;
            m_Pso->AddRayTracingShaderGroup(hitGroup);

            RHI::RHIRayTracingShaderGroup shadowMissGroup{};
            shadowMissGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            shadowMissGroup.generalShaderIndex = 3;
            m_Pso->AddRayTracingShaderGroup(shadowMissGroup);

            m_Pso->SetMaxRecursionDepth(2);

            m_Pso->UpdateDescriptorSet(0, 0, Containers::Vector<RHI::RHIAccelerationStructureHandle>{m_Tlas});

            RHI::RHIDescriptorImageInfo storageInfo{};
            storageInfo.imageView = m_StorageImageView;
            storageInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
            m_Pso->UpdateDescriptorSet(0, 1, Containers::Vector<RHI::RHIDescriptorImageInfo>{storageInfo});

            RHI::RHIDescriptorImageInfo accumInfo{};
            accumInfo.imageView = m_AccumulationImageView;
            accumInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
            m_Pso->UpdateDescriptorSet(0, 9, Containers::Vector<RHI::RHIDescriptorImageInfo>{accumInfo});

            m_Pso->UpdateDescriptorSet(0, 2, Containers::Vector<RHI::RHIBufferHandle>{m_CameraBuffers[0]});
            m_Pso->UpdateDescriptorSet(0, 3, Containers::Vector<RHI::RHIBufferHandle>{m_Model.vertexBuffer});
            m_Pso->UpdateDescriptorSet(0, 4, Containers::Vector<RHI::RHIBufferHandle>{m_Model.indexBuffer});
            m_Pso->UpdateDescriptorSet(0, 5, Containers::Vector<RHI::RHIBufferHandle>{m_MaterialBuffer});
            m_Pso->UpdateDescriptorSet(0, 6, Containers::Vector<RHI::RHIBufferHandle>{m_TriangleMaterialBuffer});

            Containers::Vector<RHI::RHIDescriptorImageInfo> modelTextures;
            modelTextures.resize(100);
            for (UInt32 i = 0; i < 100; ++i)
            {
                if (i < m_ModelTextures.size())
                {
                    modelTextures[i].imageView = m_ModelTextures[i];
                }
                else if (!m_ModelTextures.empty())
                {
                    modelTextures[i].imageView = m_ModelTextures[0];
                }
                modelTextures[i].imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            }
            m_Pso->UpdateDescriptorSet(0, 7, std::move(modelTextures));

            RHI::RHIDescriptorImageInfo samplerInfo{};
            samplerInfo.sampler = m_DefaultSampler;
            m_Pso->UpdateDescriptorSet(0, 8, Containers::Vector<RHI::RHIDescriptorImageInfo>{samplerInfo});

            m_Pso->BuildDescriptorSetLayout();

            m_Pipeline = pm->GetRayTracingPipeline(m_Pso.get());

            // Pre-allocate descriptor sets for each frame
            m_DescriptorSetIndices.resize(m_MaxFramesInFlight);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                UInt32 poolId = m_DescriptorPool->AddPool({
                                                              RHI::DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_IMAGE,
                                                              RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER,
                                                              RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,
                                                              RHI::DESCRIPTOR_TYPE_SAMPLER,
                                                              RHI::DESCRIPTOR_TYPE_STORAGE_IMAGE
                                                          }, {1, 1, 1, 1, 1, 1, 1, 100, 1, 1}, 1);
                m_DescriptorPoolIds.push_back(poolId);

                m_Pso->UpdateDescriptorSet(0, 2, Containers::Vector<RHI::RHIBufferHandle>{m_CameraBuffers[i]});
                m_DescriptorSetIndices[i] = m_DescriptorPool->AllocDescriptorSet(
                    poolId, (UInt32)0, (RHI::RHIPipelineState*)m_Pso.get());
                m_DescriptorPool->UpdateDescriptorSet(poolId, m_DescriptorSetIndices[i], m_Pso.get());
            }
        }

        void CreateSBT()
        {
            UInt32 handleSize = 32;
            UInt32 groupStride = 64;
            UInt32 sbtSize = groupStride * 4;

            RHI::RHIBufferDescriptor sbtDesc{};
            sbtSize = groupStride * 4;
            sbtDesc.size = sbtSize;
            sbtDesc.usage = RHI::BUFFER_USAGE_SHADER_BINDING_TABLE_BIT_KHR |
                RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            sbtDesc.memoryUsage = RHI::ERHIMemoryUsage::Upload;
            m_SbtBuffer = m_Device->GetFactory()->CreateBuffer(std::move(sbtDesc), "SBT Buffer");

            uint8_t* pSbtData = (uint8_t*)m_Device->GetFactory()->MapBuffer(m_SbtBuffer);

            std::vector<uint8_t> tempHandles(handleSize * 4);
            m_Device->GetRayTracing()->GetRayTracingShaderGroupHandles(m_Pipeline, 0, 4, tempHandles.size(),
                                                                       tempHandles.data());

            std::memset(pSbtData, 0, sbtSize);
            std::memcpy(pSbtData + 0 * groupStride, tempHandles.data() + 0 * handleSize, handleSize); // RayGen
            std::memcpy(pSbtData + 1 * groupStride, tempHandles.data() + 1 * handleSize, handleSize); // Miss
            std::memcpy(pSbtData + 2 * groupStride, tempHandles.data() + 3 * handleSize, handleSize);
            // ShadowMiss (Handle index 3)
            std::memcpy(pSbtData + 3 * groupStride, tempHandles.data() + 2 * handleSize, handleSize);
            // ClosestHit (Handle index 2)

            m_Device->GetFactory()->UnmapBuffer(m_SbtBuffer);
        }

        void UpdateCameraData()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);

            CameraData data;
            data.viewInverse = glm::inverse(GetViewMatrix());
            data.projInverse = glm::inverse(GetProjectionMatrix((float)width / (float)height));
            float epsilon = 0.0001f;
            bool cameraMoved = glm::distance(m_CameraPos, m_PrevCameraPos) > epsilon ||
                glm::distance(m_CameraRot, m_PrevCameraRot) > epsilon;

            if (cameraMoved)
            {
                m_AccumulatedFrames = 0;
                m_PrevCameraPos = m_CameraPos;
                m_PrevCameraRot = m_CameraRot;
            }

            data.cameraPos = glm::vec4(m_CameraPos, 1.0f);
            data.lightPosAndFrameCount = glm::vec4(10.0f, 40.0f, 10.0f, (float)m_AccumulatedFrames);

            data.numPointLights = 4;
            data.pointLights[0].posRange = glm::vec4(0.0f, 2.0f, 0.0f, 50.0f);
            data.pointLights[0].colorInt = glm::vec4(1.0f, 0.9f, 0.8f, 100.0f);
            data.pointLights[1].posRange = glm::vec4(-10.0f, 5.0f, 2.0f, 40.0f);
            data.pointLights[1].colorInt = glm::vec4(1.0f, 0.8f, 0.6f, 80.0f);
            data.pointLights[2].posRange = glm::vec4(10.0f, 5.0f, 2.0f, 40.0f);
            data.pointLights[2].colorInt = glm::vec4(0.8f, 0.9f, 1.0f, 80.0f);
            data.pointLights[3].posRange = glm::vec4(0.0f, 5.0f, -15.0f, 40.0f);
            data.pointLights[3].colorInt = glm::vec4(0.8f, 1.0f, 0.8f, 80.0f);

            m_AccumulatedFrames++;

            m_Device->GetFactory()->BufferMemoryCopy(m_CameraBuffers[GetCurrentFrameIndex()], &data, sizeof(CameraData),
                                                     0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto pool = m_Device->GetCommandBufferPool(m_CmdPool);
            auto cmdHandle = pool->GetCommandBuffer(currentIndex);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            cmd->Begin(currentIndex, 0);

            m_Pso->UpdateDescriptorSet(0, 2, Containers::Vector<RHI::RHIBufferHandle>{m_CameraBuffers[currentIndex]});
            m_DescriptorPool->UpdateDescriptorSet(m_DescriptorPoolIds[currentIndex],
                                                  m_DescriptorSetIndices[currentIndex], m_Pso.get());

            cmd->BindPipeline(m_Pipeline);
            cmd->BindDescriptorSet(RHI::PIPELINE_BIND_POINT_RAY_TRACING_KHR, 0, m_DescriptorPoolHandle,
                                   m_DescriptorPoolIds[currentIndex], 0);

            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            cmd->TransitionImageLayout(m_StorageImage, RHI::IMAGE_LAYOUT_GENERAL);
            cmd->TransitionImageLayout(m_AccumulationImage, RHI::IMAGE_LAYOUT_GENERAL);

            RHI::RHIImageMemoryBarrier accumBarrier{};
            accumBarrier.image = m_AccumulationImage;
            accumBarrier.srcAccess = RHI::ACCESS_SHADER_WRITE_BIT;
            accumBarrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            accumBarrier.oldLayout = RHI::IMAGE_LAYOUT_GENERAL;
            accumBarrier.newLayout = RHI::IMAGE_LAYOUT_GENERAL;
            accumBarrier.srcQueueFamilyIndex = 0xFFFFFFFF;
            accumBarrier.dstQueueFamilyIndex = 0xFFFFFFFF;
            accumBarrier.subresourceRange = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1};
            accumBarrier.srcStageMask = RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR;
            accumBarrier.dstStageMask = RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR;

            cmd->PipelineBarrier(RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR,
                                 RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR, 0, {}, {accumBarrier}, {});

            if (m_AccumulatedFrames > 1)
            {
                UInt32 prevIndex = (currentIndex + m_MaxFramesInFlight - 1) % m_MaxFramesInFlight;
                if (m_FrameTickets[prevIndex] > 0)
                {
                    m_Device->GetQueue(RHI::RHIQueueType::Graphics)->WaitForTicket(m_FrameTickets[prevIndex]);
                }
            }

            RHI::RHITraceRaysDescriptor traceDesc{};
            UInt64 sbtAddr = m_Device->GetFactory()->GetBufferDeviceAddress(m_SbtBuffer);
            UInt32 groupStride = 64;

            traceDesc.raygenShaderRecord = {sbtAddr + 0 * groupStride, groupStride, groupStride};
            traceDesc.missShaderTable = {sbtAddr + 1 * groupStride, groupStride, 2 * groupStride};
            traceDesc.hitShaderTable = {sbtAddr + 3 * groupStride, groupStride, groupStride};
            traceDesc.width = width;
            traceDesc.height = height;
            traceDesc.depth = 1;

            cmd->TraceRays(traceDesc);

            auto colorBuffer = m_SwapChain->BeginFrame(currentIndex);
            if (colorBuffer.IsValid())
            {
                cmd->TransitionImageLayout(m_StorageImage, RHI::IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL);
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL);

                RHI::RHIImageCopy region{};
                region.srcSubresource = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1};
                region.srcOffset = {0, 0, 0};
                region.dstSubresource = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1};
                region.dstOffset = {0, 0, 0};
                region.extent = {width, height, 1};

                cmd->CopyImage(m_StorageImage, RHI::IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, colorBuffer,
                               RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &region);
                cmd->TransitionImageLayout(colorBuffer, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
            }

            cmd->End();

            RHI::RHISubmitDescriptor submitDesc = {};
            submitDesc.WaitSwapChain = m_SwapChain;
            submitDesc.SignalSwapChain = m_SwapChain;
            m_FrameTickets[currentIndex] = m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle, &submitDesc);

            m_SwapChain->EndFrame(currentIndex);
            pool->ReleaseCommandBuffer(currentIndex, cmdHandle);
        }

        RHI::RHIShaderProgramHandle CompileShader(const String& name, const String& entry, const String& profile)
        {
            String envStr = GetShaderEnvString();

            namespace fs = std::filesystem;
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = fs::path(exePathW).parent_path();
            String path = String::WStringToString(
                (exeDir / L"Shader" / (String::StringToWString(name) + L".hlsl")).wstring());

            HAL::ShaderCompileParams params{};
            params.input = String::StringToWString(path);
            params.entry = String::StringToWString(entry);
            params.shaderModel = String::StringToWString(profile);
            params.target = L"-spirv";
            params.targetEnv = L"vulkan1.2";
            params.optimizeLevel = L"0";
            params.stage = RHI::EProgramStage::RayTracing;

            HAL::ShaderCompilerOutput output;
            if (!HAL::CompileShaderFromFile(std::move(params), output) || output.codePointer == nullptr || output.
                codeSize == 0)
            {
                LOG_ERRORF("Failed to compile shader: {0} {1}", name.c_str(), entry.c_str());
                return {};
            }

            auto prog = m_Device->GetFactory()->CreateGPUProgram();
            RHI::RHIShaderProgramDesc desc = {
                (UInt32)output.codeSize, output.codePointer, entry.c_str(), name.c_str(), RHI::SHADER_STAGE_RAYGEN_BIT
            };

            if (entry.Contains("RayGen")) desc.stage = RHI::SHADER_STAGE_RAYGEN_BIT;
            else if (entry.Contains("Miss")) desc.stage = RHI::SHADER_STAGE_MISS_BIT;
            else if (entry.Contains("ClosestHit")) desc.stage = RHI::SHADER_STAGE_CLOSEST_HIT_BIT;

            m_Device->GetFactory()->AttachProgramByteCode(prog, std::move(desc));
            if (output.codePointer) std::free(output.codePointer);

            return prog;
        }
    };
}
