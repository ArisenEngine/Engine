#pragma once

#include "../RHIRenderingTestBase.h"
#include "../../../Engine/NativeEngine/RHI/RayTracingExports.h"
#include "RHI/Resources/RHIAccelerationStructure.h"

#include "Logger/Logger.h"

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
        
        RHI_BufferHandle m_MaterialBuffer = 0;
        RHI_BufferHandle m_TriangleMaterialBuffer = 0;
        Containers::Vector<RHI_ImageViewHandle> m_ModelTextures;
        RHI_SamplerHandle m_DefaultSampler = 0;

        Containers::Vector<RHI_BufferHandle> m_CameraBuffers;
        Containers::Vector<UInt32> m_DescriptorSetIndices;
        
        struct PointLight
        {
            glm::vec4 posRange;   // xyz: pos, w: range
            glm::vec4 colorInt;   // xyz: color, w: intensity
        };

        struct CameraData
        {
            glm::mat4 viewInverse;
            glm::mat4 projInverse;
            glm::vec4 cameraPos;             // xyz: pos, w: unused
            glm::vec4 lightPosAndFrameCount; // xyz: sunPos, w: frameCount
            PointLight pointLights[8];
            int numPointLights;
            int padding[3];
        };

        RHI_ImageHandle m_AccumulationImage = 0;
        RHI_ImageViewHandle m_AccumulationImageView = 0;
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

            auto limits = RHI_Device_GetDeviceLimits(m_Device);
            if (!limits.rayTracingSupported)
            {
                LOG_WARN("Ray Tracing extension not supported or enabled, skipping test.");
                return false; // Gracefully skip
            }

            InitCommonResources();
            
            // Check RT support (implied by API existence, but in a real engine we'd check features)
            
            CreateCommonResources();
            BuildAccelerationStructures();
            CreateSizeDependentResources();
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
            
            if (m_StorageImageView) RHI_Device_ReleaseImageView(m_Device, m_StorageImageView);
            if (m_StorageImage) RHI_Device_ReleaseImage(m_Device, m_StorageImage);
            if (m_AccumulationImageView) RHI_Device_ReleaseImageView(m_Device, m_AccumulationImageView);
            if (m_AccumulationImage) RHI_Device_ReleaseImage(m_Device, m_AccumulationImage);
            
            if (m_Pso) RHI_PSO_Release(m_Pso);
            
            if (m_MaterialBuffer) RHI_Device_ReleaseBuffer(m_Device, m_MaterialBuffer);
            if (m_TriangleMaterialBuffer) RHI_Device_ReleaseBuffer(m_Device, m_TriangleMaterialBuffer);
            if (m_DefaultSampler) RHI_Device_ReleaseSampler(m_Device, m_DefaultSampler);

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

        void OnResize(UInt32 width, UInt32 height) override
        {
            if (width == 0 || height == 0) return;

            m_AccumulatedFrames = 0;
            if (m_StorageImageView) RHI_Device_ReleaseImageView(m_Device, m_StorageImageView);
            if (m_StorageImage) RHI_Device_ReleaseImage(m_Device, m_StorageImage);
            if (m_AccumulationImageView) RHI_Device_ReleaseImageView(m_Device, m_AccumulationImageView);
            if (m_AccumulationImage) RHI_Device_ReleaseImage(m_Device, m_AccumulationImage);

            m_StorageImageView = 0;
            m_StorageImage = 0;
            m_AccumulationImageView = 0;
            m_AccumulationImage = 0;

            CreateSizeDependentResources();

            // Update descriptors for images in PSO
            if (m_Pso)
            {
                Containers::Vector<RHI::RHIDescriptorImageInfo> images;
                RHI::RHIDescriptorImageInfo storageInfo{};
                storageInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_StorageImageView);
                storageInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
                images.push_back(storageInfo);
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 1, &images);

                images.clear();
                RHI::RHIDescriptorImageInfo accumInfo{};
                accumInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_AccumulationImageView);
                accumInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
                images.push_back(accumInfo);
                RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 9, &images);
            }
        }

    private:
        void CreateCommonResources()
        {
            wchar_t exePathW[MAX_PATH]{};
            GetModuleFileNameW(nullptr, exePathW, MAX_PATH);
            auto exeDir = std::filesystem::path(exePathW).parent_path();
            
            std::filesystem::path modelPath = exeDir / "Assets" / "glTF-Sample-Models" / "2.0" / "Sponza" / "glTF" / "Sponza.gltf";
            m_Model = LoadGLTF(modelPath.string());

            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                RHI::RHIBufferDescriptor cbDesc = {};
                cbDesc.size = sizeof(CameraData);
                cbDesc.usage = RHI::BUFFER_USAGE_UNIFORM_BUFFER_BIT;
                cbDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
                m_CameraBuffers.push_back(RHI_Device_CreateBuffer(m_Device, &cbDesc, "Camera CB"));
            }

            // Material and Primitive Data
            m_ModelTextures.clear();
            Containers::Vector<MaterialData> matData;
            for (auto& mat : m_Model.materials)
            {
                MaterialData md{};
                md.baseColorFactor = mat.baseColorFactor;
                
                // Only add texture if valid and we have space in the shader array
                if (mat.baseColorView != 0 && m_ModelTextures.size() < 100)
                {
                    md.baseColorTextureIndex = (int)m_ModelTextures.size();
                    m_ModelTextures.push_back(mat.baseColorView);
                }
                else
                {
                    md.baseColorTextureIndex = -1;
                    if (mat.baseColorView != 0)
                    {
                        LOG_WARN("Material texture skipped due to limit (100).");
                    }
                }

                md.metallicFactor = 0.0f;
                md.roughnessFactor = 1.0f;
                matData.push_back(md);
                
                LOG_INFOF("Material {0}: baseColorFactor=({1},{2},{3},{4}), texIdx={5}", 
                    matData.size() - 1,
                    md.baseColorFactor.r, md.baseColorFactor.g, md.baseColorFactor.b, md.baseColorFactor.a,
                    md.baseColorTextureIndex);
            }


            RHI::RHIBufferDescriptor matBufDesc{};
            matBufDesc.size = matData.size() * sizeof(MaterialData);
            matBufDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT | RHI::BUFFER_USAGE_TRANSFER_DST_BIT;
            matBufDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_MaterialBuffer = RHI_Device_CreateBuffer(m_Device, &matBufDesc, "Material Buffer");
            
            // Create staging buffer for upload
            RHI::RHIBufferDescriptor stagingDesc{};
            stagingDesc.size = matBufDesc.size;
            stagingDesc.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
            stagingDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            auto stagingBuffer = RHI_Device_CreateBuffer(m_Device, &stagingDesc, "Material Staging Buffer");
            RHI_Buffer_MemoryCopy(m_Device, stagingBuffer, matData.data(), stagingDesc.size, 0);
            
            // Copy from staging to device local buffer
            auto cmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, cmdPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            RHI_Cmd_CopyBuffer(cmd, stagingBuffer, 0, m_MaterialBuffer, 0, matBufDesc.size);
            RHI_Cmd_End(cmd);
            RHI_Device_Submit(m_Device, cmd, 0);
            RHI_Device_WaitIdle(m_Device);
            RHI_Device_ReleaseCommandBuffer(m_Device, cmdPool, 0, cmd);
            RHI_Device_ReleaseCommandBufferPool(m_Device, cmdPool);
            RHI_Device_ReleaseBuffer(m_Device, stagingBuffer);

            // Per-Submesh Data Buffer
            Containers::Vector<SubmeshData> submeshData;
            LOG_INFOF("Populating SubmeshData for {0} primitives...", m_Model.primitives.size());
            for (size_t i = 0; i < m_Model.primitives.size(); ++i)
            {
                const auto& prim = m_Model.primitives[i];
                submeshData.push_back({ (UInt32)prim.materialIndex, prim.firstIndex, {0, 0} });
            }

            RHI::RHIBufferDescriptor triBufDesc{};
            triBufDesc.size = submeshData.size() * sizeof(SubmeshData);
            triBufDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            triBufDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_TriangleMaterialBuffer = RHI_Device_CreateBuffer(m_Device, &triBufDesc, "Submesh Data Buffer");
            RHI_Buffer_MemoryCopy(m_Device, m_TriangleMaterialBuffer, submeshData.data(), triBufDesc.size, 0);

            LOG_INFOF("Total unique textures loaded: {0}", m_ModelTextures.size());

            m_CameraPos = glm::vec3(0.0f, 5.0f, 10.0f);
            m_CameraRot = glm::vec3(0.0f, -glm::half_pi<float>(), 0.0f); // Face forward (adjust if needed)
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
            m_DefaultSampler = RHI_Device_CreateSampler(m_Device, &sampDesc);

            LOG_INFOF("Camera initialized at ({0}, {1}, {2})", m_CameraPos.x, m_CameraPos.y, m_CameraPos.z);
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
            imgDesc.usage = RHI::IMAGE_USAGE_STORAGE_BIT | RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT;
            imgDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            imgDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
            m_StorageImage = RHI_Device_CreateImage(m_Device, &imgDesc, "RT Storage Image");

            RHI::RHIImageViewDesc viewDesc = {};
            viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
            viewDesc.format = RHI::FORMAT_B8G8R8A8_UNORM;
            viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            viewDesc.levelCount = 1;
            viewDesc.layerCount = 1;
            viewDesc.width = width;
            viewDesc.height = height;
            m_StorageImageView = RHI_Image_AddImageView(m_Device, m_StorageImage, &viewDesc);

            // Accumulation Image (32-bit float for high precision)
            imgDesc.format = RHI::FORMAT_R32G32B32A32_SFLOAT;
            imgDesc.usage = RHI::IMAGE_USAGE_STORAGE_BIT | RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT;
            m_AccumulationImage = RHI_Device_CreateImage(m_Device, &imgDesc, "RT Accumulation Image");

            viewDesc.format = RHI::FORMAT_R32G32B32A32_SFLOAT;
            m_AccumulationImageView = RHI_Image_AddImageView(m_Device, m_AccumulationImage, &viewDesc);
        }

        void BuildAccelerationStructures()
        {
            // 1. BLAS
            // We now use multiple geometries (one per primitive) to allow robust material lookup via GeometryIndex()
            Containers::Vector<RHI::RHIAccelerationStructureGeometryData> geometries;
            Containers::Vector<UInt32> maxPrimCounts;
            Containers::Vector<RHI::RHIAccelerationStructureBuildRangeInfo> buildRanges;

            for (const auto& prim : m_Model.primitives)
            {
                RHI::RHIAccelerationStructureGeometryData geom{};
                geom.type = RHI::ERHIAccelerationStructureGeometryType::Triangles;
                geom.flags = RHI::AS_GEOMETRY_OPAQUE_BIT;
                geom.triangles.vertexFormat = RHI::FORMAT_R32G32B32_SFLOAT;
                geom.triangles.vertexData = RHI_Buffer_GetDeviceAddress(m_Device, m_Model.vertexBuffer);
                geom.triangles.vertexStride = sizeof(GLTFVertex); // 64 bytes
                geom.triangles.maxVertex = (UInt32)m_Model.vertexCount;
                geom.triangles.indexType = RHI::INDEX_TYPE_UINT32;
                geom.triangles.indexData = RHI_Buffer_GetDeviceAddress(m_Device, m_Model.indexBuffer);
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

            LOG_INFOF("Building BLAS for model: {0} vertices, {1} indices, {2} geometries", 
                m_Model.vertexCount, m_Model.indexCount, geometries.size());

            RHI::RHIAccelerationStructureBuildSizesInfo blasSizes{};
            RHI_Device_GetAccelerationStructureBuildSizes(m_Device, &blasInfo, maxPrimCounts.data(), &blasSizes);

            if (blasSizes.accelerationStructureSize == 0)
            {
                LOG_ERROR("BLAS build size is 0! Check model data.");
            }
            else
            {
                LOG_INFOF("BLAS Build Sizes: AS={0}, Scratch={1}", blasSizes.accelerationStructureSize, blasSizes.buildScratchSize);
            }

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
            
            const RHI::RHIAccelerationStructureBuildRangeInfo* pBlasRanges = buildRanges.data();
            RHI_Cmd_BuildAccelerationStructures(cmd, 1, &blasInfo, &pBlasRanges);

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
            auto smiss = CompileShader(L"RayTracingTest", "ShadowMiss", "6_3");

            RHI_PSO_AddProgram(m_Pso, rgen);
            RHI_PSO_AddProgram(m_Pso, rmiss);
            RHI_PSO_AddProgram(m_Pso, rchit);
            RHI_PSO_AddProgram(m_Pso, smiss);

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

            RHI::RHIRayTracingShaderGroup shadowMissGroup{};
            shadowMissGroup.type = RHI::ERHIRayTracingShaderGroupType::General;
            shadowMissGroup.generalShaderIndex = 3;
            RHI_PSO_AddRayTracingShaderGroup(m_Pso, &shadowMissGroup);

            RHI_PSO_SetMaxRecursionDepth(m_Pso, 2); // Increased to 2 for shadow rays in ClosestHit

            // Descriptors Layout are automatically handled via shader reflection in RHI_PSO_AddProgram

            // Update descriptors for initial setup (needed for reflector/builder if it's dynamic)
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
            
            images.clear();
            RHI::RHIDescriptorImageInfo accumInfo{};
            accumInfo.imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_AccumulationImageView);
            accumInfo.imageLayout = RHI::IMAGE_LAYOUT_GENERAL;
            images.push_back(accumInfo);
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 9, &images);

            Containers::Vector<RHI_BufferHandle> ubos = { m_CameraBuffers[0] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 2, &ubos);

            Containers::Vector<RHI_BufferHandle> vb = { m_Model.vertexBuffer };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 3, &vb);
            
            Containers::Vector<RHI_BufferHandle> ib = { m_Model.indexBuffer };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 4, &ib);

            Containers::Vector<RHI_BufferHandle> mb = { m_MaterialBuffer };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 5, &mb);

            Containers::Vector<RHI_BufferHandle> pb = { m_TriangleMaterialBuffer };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 6, &pb);

            // Fill texture array (exactly 100 as in shader)
            Containers::Vector<RHI::RHIDescriptorImageInfo> modelTextures;
            modelTextures.resize(100);
            for (UInt32 i = 0; i < 100; ++i)
            {
                if (i < m_ModelTextures.size())
                {
                    modelTextures[i].imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_ModelTextures[i]);
                }
                else if (!m_ModelTextures.empty())
                {
                    modelTextures[i].imageView = *reinterpret_cast<RHI::RHIImageViewHandle*>(&m_ModelTextures[0]);
                }
                modelTextures[i].imageLayout = RHI::IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
            }
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 7, &modelTextures);

            Containers::Vector<RHI::RHIDescriptorImageInfo> defaultSamplers;
            RHI::RHIDescriptorImageInfo samplerInfo{};
            samplerInfo.sampler = *reinterpret_cast<RHI::RHISamplerHandle*>(&m_DefaultSampler);
            defaultSamplers.push_back(samplerInfo);
            RHI_PSO_UpdateDescriptorSet_Images(m_Pso, 0, 8, &defaultSamplers);

            RHI_PSO_BuildDescriptorSetLayout(m_Pso);

            LOG_INFOF("RayTracing DescriptorPool: {0} textures", m_ModelTextures.size());

            // Initialize descriptor pools for each frame in flight
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                Containers::Vector<RHI::EDescriptorType> types = {
                    RHI::DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR,
                    RHI::DESCRIPTOR_TYPE_STORAGE_IMAGE,
                    RHI::DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, // VB
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, // IB
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, // Mat
                    RHI::DESCRIPTOR_TYPE_STORAGE_BUFFER, // Prim
                    RHI::DESCRIPTOR_TYPE_SAMPLED_IMAGE,   // Textures
                    RHI::DESCRIPTOR_TYPE_SAMPLER,         // DefaultSampler
                    RHI::DESCRIPTOR_TYPE_STORAGE_IMAGE    // Accumulation
                };
                Containers::Vector<UInt32> counts = { 1, 1, 1, 1, 1, 1, 1, 100, 1, 1 };
                m_DescriptorPoolIds.push_back(RHI_DescriptorPool_AddPool(m_DescriptorPool, &types, &counts, 1));
            }

            // Pre-allocate descriptor sets for each frame
            m_DescriptorSetIndices.resize(m_MaxFramesInFlight);
            for (UInt32 i = 0; i < m_MaxFramesInFlight; ++i)
            {
                UInt32 poolId = m_DescriptorPoolIds[i];
                
                // Update camera buffer for this frame
                Containers::Vector<RHI_BufferHandle> ubos = { m_CameraBuffers[i] };
                RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 2, &ubos);
                
                // Allocate descriptor set
                m_DescriptorSetIndices[i] = RHI_DescriptorPool_AllocDescriptorSet(m_DescriptorPool, poolId, 0, m_Pso);
                
                // Update all descriptors (must update ALL resources including textures!)
                RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, poolId, m_DescriptorSetIndices[i], m_Pso);
            }

            m_Pipeline = RHI_PipelineManager_GetRayTracingPipeline(pm, m_Pso);
        }


        void CreateSBT()
        {
            // SBT must be aligned to shaderGroupBaseAlignment (usually 64 bytes)
            UInt32 handleSize = 32;          // Size of the shader group handle (device property, usually 32)
            UInt32 groupStride = 64;         // Stride must be aligned to shaderGroupBaseAlignment
            UInt32 sbtSize = groupStride * 4;

            RHI::RHIBufferDescriptor sbtDesc{};
            sbtDesc.size = sbtSize;
            sbtDesc.usage = RHI::BUFFER_USAGE_SHADER_BINDING_TABLE_BIT_KHR | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT;
            sbtDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
            m_SbtBuffer = RHI_Device_CreateBuffer(m_Device, &sbtDesc, "SBT Buffer");

            uint8_t* pSbtData = (uint8_t*)RHI_Buffer_Map(m_Device, m_SbtBuffer);
            
            // Get handles in a temp buffer
            std::vector<uint8_t> tempHandles(handleSize * 4);
            RHI_Device_GetRayTracingShaderGroupHandles(m_Device, m_Pipeline, 0, 4, tempHandles.size(), tempHandles.data());
            
            // Write to SBT with alignment padding
            // Layout: RayGen, Miss, Hit, ShadowMiss (as added above)
            // Wait, I added them in order: RayGen(0), Miss(1), Hit(2), ShadowMiss(3).
            // So indices are 0, 1, 2, 3.
            std::memset(pSbtData, 0, sbtSize);
            std::memcpy(pSbtData + 0 * groupStride, tempHandles.data() + 0 * handleSize, handleSize); // RayGen
            std::memcpy(pSbtData + 1 * groupStride, tempHandles.data() + 1 * handleSize, handleSize); // Miss
            std::memcpy(pSbtData + 2 * groupStride, tempHandles.data() + 3 * handleSize, handleSize); // ShadowMiss (Handle index 3)
            std::memcpy(pSbtData + 3 * groupStride, tempHandles.data() + 2 * handleSize, handleSize); // ClosestHit (Handle index 2)

            RHI_Buffer_Unmap(m_Device, m_SbtBuffer);
        }

        void UpdateCameraData()
        {
            UpdateCamera((float)frameTime);
            float width = (float)HAL::GetWindowWidth(m_WindowId);
            float height = (float)HAL::GetWindowHeight(m_WindowId);

            CameraData data;
            data.viewInverse = glm::inverse(GetViewMatrix());
            data.projInverse = glm::inverse(GetProjectionMatrix((float)width / (float)height));
            // Camera movement detection with epsilon
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
            
            // Set up some point lights in Sponza with MUCH higher intensity
            data.numPointLights = 4;
            // 1. Center low
            data.pointLights[0].posRange = glm::vec4(0.0f, 2.0f, 0.0f, 50.0f);
            data.pointLights[0].colorInt = glm::vec4(1.0f, 0.9f, 0.8f, 100.0f);
            // 2. Left corridor
            data.pointLights[1].posRange = glm::vec4(-10.0f, 5.0f, 2.0f, 40.0f);
            data.pointLights[1].colorInt = glm::vec4(1.0f, 0.8f, 0.6f, 80.0f);
            // 3. Right corridor
            data.pointLights[2].posRange = glm::vec4(10.0f, 5.0f, 2.0f, 40.0f);
            data.pointLights[2].colorInt = glm::vec4(0.8f, 0.9f, 1.0f, 80.0f);
            // 4. Far end
            data.pointLights[3].posRange = glm::vec4(0.0f, 5.0f, -15.0f, 40.0f);
            data.pointLights[3].colorInt = glm::vec4(0.8f, 1.0f, 0.8f, 80.0f);

            m_AccumulatedFrames++;
            
            RHI_Buffer_MemoryCopy(m_Device, m_CameraBuffers[GetCurrentFrameIndex()], &data, sizeof(CameraData), 0);
        }

        void RecordAndSubmit()
        {
            auto currentIndex = GetCurrentFrameIndex();
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);
            
            RHI_Cmd_Begin(cmd, currentIndex, 0);

            // Update camera buffer for current frame
            Containers::Vector<RHI_BufferHandle> ubos = { m_CameraBuffers[currentIndex] };
            RHI_PSO_UpdateDescriptorSet_Buffers(m_Pso, 0, 2, &ubos);
            
            // Update the pre-allocated descriptor set
            UInt32 poolId = m_DescriptorPoolIds[currentIndex];
            UInt32 setIdx = m_DescriptorSetIndices[currentIndex];
            RHI_DescriptorPool_UpdateDescriptorSet(m_DescriptorPool, poolId, setIdx, m_Pso);

            RHI_Cmd_BindPipeline(cmd, m_Pipeline);
            RHI_Cmd_BindDescriptorSet_FromPool(cmd, RHI::PIPELINE_BIND_POINT_RAY_TRACING_KHR, 0, m_DescriptorPool, poolId, setIdx);

            if (m_Tlas == 0)
            {
               LOG_ERROR("TLAS handle is 0 in RecordAndSubmit!");
            }


            UInt32 width = HAL::GetWindowWidth(m_WindowId);
            UInt32 height = HAL::GetWindowHeight(m_WindowId);

            RHI_Cmd_TransitionImageLayout(cmd, m_StorageImage, RHI::IMAGE_LAYOUT_GENERAL);
            RHI_Cmd_TransitionImageLayout(cmd, m_AccumulationImage, RHI::IMAGE_LAYOUT_GENERAL);
            
            RHI::RHIImageMemoryBarrier accumBarrier{};
            accumBarrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&m_AccumulationImage);
            accumBarrier.srcAccess = RHI::ACCESS_SHADER_WRITE_BIT;
            accumBarrier.dstAccess = RHI::ACCESS_SHADER_READ_BIT;
            accumBarrier.oldLayout = RHI::IMAGE_LAYOUT_GENERAL;
            accumBarrier.newLayout = RHI::IMAGE_LAYOUT_GENERAL;
            accumBarrier.srcQueueFamilyIndex = 0x7FFFFFFF;
            accumBarrier.dstQueueFamilyIndex = 0x7FFFFFFF;
            accumBarrier.subresourceRange.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
            accumBarrier.subresourceRange.baseMipLevel = 0;
            accumBarrier.subresourceRange.levelCount = 1;
            accumBarrier.subresourceRange.baseArrayLayer = 0;
            accumBarrier.subresourceRange.layerCount = 1;
            accumBarrier.srcStageMask = RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR;
            accumBarrier.dstStageMask = RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR;
            
            Containers::Vector<RHI::RHIImageMemoryBarrier> accumBarriers;
            accumBarriers.push_back(accumBarrier);
            RHI_Cmd_PipelineBarrier_Image(cmd, (UInt32)RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR, (UInt32)RHI::PIPELINE_STAGE_RAY_TRACING_SHADER_BIT_KHR, 0, &accumBarriers);

            // If we are accumulating, we need to ensure THE ENTIRE previous frame's work on this image is done.
            // In a multi-frame-in-flight scenario, we might need a more robust sync if we want to avoid bubbles,
            // but for this test, waiting for the previous frame's ticket is the simplest fix for the race.
            if (m_AccumulatedFrames > 1)
            {
                UInt32 prevIndex = (currentIndex + m_MaxFramesInFlight - 1) % m_MaxFramesInFlight;
                if (m_FrameTickets[prevIndex] > 0)
                {
                    RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[prevIndex]);
                }
            }

            RHI::RHITraceRaysDescriptor traceDesc{};
            UInt64 sbtAddr = RHI_Buffer_GetDeviceAddress(m_Device, m_SbtBuffer);
            UInt32 groupStride = 64;

            traceDesc.raygenShaderRecord = { sbtAddr + 0 * groupStride, groupStride, groupStride };
            traceDesc.missShaderTable = { sbtAddr + 1 * groupStride, groupStride, 2 * groupStride };
            traceDesc.hitShaderTable = { sbtAddr + 3 * groupStride, groupStride, groupStride };
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
                LOG_ERROR(output.msgOut.c_str());
                Diagnostics::Log::Error("Shader compilation failed. Flushing...");
                Diagnostics::Logger::Shutdown(); // Forced flush and shut down to ensure logs are on disk
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
