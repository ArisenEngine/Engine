#define GLM_ENABLE_EXPERIMENTAL
#define CGLTF_IMPLEMENTATION
#include <cgltf.h>
#include "stb_image.h"
#include "RHITestBase.h"
#include <iostream>
#include <filesystem>
#include "RHI/Enums/Buffer/EBufferUsage.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Queues/RHIQueueType.h"
#include "RHI/Core/RHIFactory.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Diagnostics/RHIError.h"
#include "RHI/Samplers/RHISampler.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include <functional>
#include <glm/gtc/type_ptr.hpp>
#include <glm/gtx/quaternion.hpp>
#include <map>


using namespace ArisenEngine;

namespace ArisenEngine::Testing
{
    GLTFModel RHITestBase::LoadGLTF(const String& path)
    {
        cgltf_options options = {};
        cgltf_data* data = nullptr;
        cgltf_result result = cgltf_parse_file(&options, path.c_str(), &data);

        if (result != cgltf_result_success)
        {
            LOG_ERRORF("Failed to parse glTF file: {0}", path);
            return {};
        }

        result = cgltf_load_buffers(&options, data, path.c_str());
        if (result != cgltf_result_success)
        {
            LOG_ERRORF("Failed to load glTF buffers for: {0}", path);
            cgltf_free(data);
            return {};
        }

        std::vector<GLTFVertex> vertices;
        std::vector<uint32_t> indices;

        glm::vec3 minBound(1e10f), maxBound(-1e10f);

        GLTFModel model;

        std::filesystem::path modelPath(path.c_str());
        std::filesystem::path modelDir = modelPath.parent_path();

        // Helper for image uploading and mipmap generation
        auto uploadAndMipmap = [&](RHI::RHIImageHandle texture, UInt32 width, UInt32 height, void* data,
                                   UInt32 mipLevels)
        {
            // Create staging buffer
            UInt32 size = width * height * 4;
            RHI::RHIBufferDescriptor stagingDesc{
                0, size, RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr,
                RHI::ERHIMemoryUsage::Upload
            };
            RHI::RHIBufferHandle stagingBuffer = m_Device->GetFactory()->CreateBuffer(
                std::move(stagingDesc), "Texture Staging Buffer");

            void* mapped = m_Device->GetFactory()->MapBuffer(stagingBuffer);
            memcpy(mapped, data, size);
            m_Device->GetFactory()->UnmapBuffer(stagingBuffer);

            auto cmdPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
            auto cmdHandle = m_Device->GetCommandBufferPool(cmdPool)->GetCommandBuffer(0);
            auto cmd = m_Device->GetCommandBuffer(cmdHandle);

            cmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);

            // Transition to DST
            {
                RHI::RHIImageMemoryBarrier barrier{};
                barrier.srcAccess = RHI::ACCESS_NONE;
                barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                barrier.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                barrier.image = texture;
                barrier.subresourceRange = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, mipLevels, 0, 1};
                barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;

                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers{barrier};
                cmd->PipelineBarrier(RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0,
                                     std::move(barriers));
            }

            // Copy
            {
                RHI::RHIBufferImageCopy region{};
                region.bufferOffset = 0;
                region.bufferRowLength = 0;
                region.bufferImageHeight = 0;
                region.imageSubresource = {RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1};
                region.offsetX = 0;
                region.offsetY = 0;
                region.offsetZ = 0;
                region.width = width;
                region.height = height;
                region.depth = 1;

                Containers::Vector<RHI::RHIBufferImageCopy> regions;
                regions.push_back(region);
                cmd->CopyBufferToImage(stagingBuffer, texture, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL,
                                       std::move(regions));
            }

            // Generate Mipmaps
            cmd->GenerateMipmaps(texture);

            cmd->End();
            m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle);
            m_Device->DeviceWaitIdle();

            m_Device->GetFactory()->ReleaseBuffer(stagingBuffer);
            m_Device->GetCommandBufferPool(cmdPool)->ReleaseCommandBuffer(0, cmdHandle);
            m_Device->GetFactory()->ReleaseCommandBufferPool(cmdPool);
        };

        // Load Materials
        std::map<std::string, RHI::RHIImageHandle> textureCache;
        std::map<std::string, RHI::RHIImageViewHandle> textureViewCache;

        for (cgltf_size i = 0; i < data->materials_count; ++i)
        {
            cgltf_material& mat = data->materials[i];
            GLTFMaterial gMat;

            // Read base color factor
            if (mat.has_pbr_metallic_roughness)
            {
                gMat.baseColorFactor = glm::vec4(
                    mat.pbr_metallic_roughness.base_color_factor[0],
                    mat.pbr_metallic_roughness.base_color_factor[1],
                    mat.pbr_metallic_roughness.base_color_factor[2],
                    mat.pbr_metallic_roughness.base_color_factor[3]
                );
            }

            if (mat.has_pbr_metallic_roughness && mat.pbr_metallic_roughness.base_color_texture.texture)
            {
                cgltf_texture* tex = mat.pbr_metallic_roughness.base_color_texture.texture;
                if (tex->image && tex->image->uri)
                {
                    String texPath = (modelDir / tex->image->uri).string().c_str();

                    // Check cache to avoid duplicate texture creation
                    if (textureCache.find(texPath.c_str()) != textureCache.end())
                    {
                        gMat.baseColorTexture = textureCache[texPath.c_str()];
                        gMat.baseColorView = textureViewCache[texPath.c_str()];
                    }
                    else
                    {
                        int tw, th, tc;
                        stbi_uc* pixels = stbi_load(texPath.c_str(), &tw, &th, &tc, STBI_rgb_alpha);
                        if (pixels)
                        {
                            RHI::RHIImageDescriptor texDesc = {};
                            texDesc.imageType = RHI::IMAGE_TYPE_2D;
                            texDesc.width = (UInt32)tw;
                            texDesc.height = (UInt32)th;
                            texDesc.depth = 1;
                            texDesc.mipLevels = static_cast<uint32_t>(std::floor(std::log2((std::max)(tw, th)))) + 1;
                            texDesc.arrayLayers = 1;
                            texDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                            texDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
                            texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT |
                                RHI::IMAGE_USAGE_SAMPLED_BIT;
                            texDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
                            texDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
                            gMat.baseColorTexture = m_Device->GetFactory()->CreateImage(
                                std::move(texDesc), tex->image->uri);

                            RHI::RHIImageViewDesc viewDesc = {};
                            viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                            viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                            viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                            viewDesc.levelCount = texDesc.mipLevels;
                            viewDesc.layerCount = 1;
                            gMat.baseColorView = m_Device->GetFactory()->CreateImageView(
                                gMat.baseColorTexture, std::move(viewDesc));

                            uploadAndMipmap(gMat.baseColorTexture, tw, th, pixels, texDesc.mipLevels);
                            stbi_image_free(pixels);

                            // Add to cache
                            textureCache[texPath.c_str()] = gMat.baseColorTexture;
                            textureViewCache[texPath.c_str()] = gMat.baseColorView;
                        }
                        else
                        {
                            LOG_ERRORF("Failed to load texture: {0}", texPath.c_str());
                        }
                    }
                }
            }

            // Create Fallback if loading failed
            if (!gMat.baseColorTexture.IsValid())
            {
                UInt32 white = 0xFFFFFFFF;
                RHI::RHIImageDescriptor texDesc = {};
                texDesc.imageType = RHI::IMAGE_TYPE_2D;
                texDesc.width = 1;
                texDesc.height = 1;
                texDesc.depth = 1;
                texDesc.mipLevels = 1;
                texDesc.arrayLayers = 1;
                texDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                texDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
                texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT |
                    RHI::IMAGE_USAGE_SAMPLED_BIT;
                texDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
                texDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;
                gMat.baseColorTexture = m_Device->GetFactory()->CreateImage(std::move(texDesc), "Fallback White");

                RHI::RHIImageViewDesc viewDesc = {};
                viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                viewDesc.levelCount = 1;
                viewDesc.layerCount = 1;
                gMat.baseColorView = m_Device->GetFactory()->
                                               CreateImageView(gMat.baseColorTexture, std::move(viewDesc));

                uploadAndMipmap(gMat.baseColorTexture, 1, 1, &white, 1);
            }

            // Default Sampler
            RHI::RHISamplerDesc sampDesc = {};
            sampDesc.magFilter = RHI::FILTER_LINEAR;
            sampDesc.minFilter = RHI::FILTER_LINEAR;
            sampDesc.mipmapMode = RHI::SAMPLER_MIPMAP_MODE_LINEAR;
            sampDesc.maxLod = 16.0f;
            sampDesc.addressModeU = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            sampDesc.addressModeV = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            sampDesc.addressModeW = RHI::SAMPLER_ADDRESS_MODE_REPEAT;
            gMat.sampler = m_Device->GetFactory()->CreateSampler(std::move(sampDesc));

            model.materials.push_back(gMat);
        }

        // Helper to traverse nodes and apply transforms
        std::function<void(cgltf_node*, const glm::mat4&)> processNode;
        processNode = [&](cgltf_node* node, const glm::mat4& parentTransform)
        {
            glm::mat4 localTransform(1.0f);
            cgltf_node_transform_local(node, glm::value_ptr(localTransform));
            glm::mat4 worldTransform = parentTransform * localTransform;

            if (node->mesh)
            {
                cgltf_mesh* mesh = node->mesh;
                for (cgltf_size j = 0; j < mesh->primitives_count; ++j)
                {
                    cgltf_primitive& primitive = mesh->primitives[j];

                    // Load attributes
                    cgltf_accessor* pos_accessor = nullptr;
                    cgltf_accessor* normal_accessor = nullptr;
                    cgltf_accessor* uv_accessor = nullptr;
                    cgltf_accessor* color_accessor = nullptr;

                    for (cgltf_size k = 0; k < primitive.attributes_count; ++k)
                    {
                        cgltf_attribute& attr = primitive.attributes[k];
                        if (attr.type == cgltf_attribute_type_position) pos_accessor = attr.data;
                        else if (attr.type == cgltf_attribute_type_normal) normal_accessor = attr.data;
                        else if (attr.type == cgltf_attribute_type_texcoord) uv_accessor = attr.data;
                        else if (attr.type == cgltf_attribute_type_color) color_accessor = attr.data;
                    }

                    if (!pos_accessor) continue;

                    size_t vertex_offset = vertices.size();
                    size_t vertex_count = pos_accessor->count;
                    vertices.resize(vertex_offset + vertex_count);

                    for (size_t v = 0; v < vertex_count; ++v)
                    {
                        GLTFVertex& vertex = vertices[vertex_offset + v];
                        vertex.color = glm::vec4(1.0f); // Default to white

                        glm::vec3 localPos;
                        cgltf_accessor_read_float(pos_accessor, v, &localPos.x, 3);
                        vertex.pos = worldTransform * glm::vec4(localPos, 1.0f);

                        minBound = glm::min(minBound, glm::vec3(vertex.pos));
                        maxBound = glm::max(maxBound, glm::vec3(vertex.pos));

                        if (normal_accessor)
                        {
                            glm::vec3 localNormal;
                            cgltf_accessor_read_float(normal_accessor, v, &localNormal.x, 3);
                            vertex.normal = glm::vec4(
                                glm::normalize(glm::vec3(worldTransform * glm::vec4(localNormal, 0.0f))), 0.0f);
                        }
                        if (uv_accessor) cgltf_accessor_read_float(uv_accessor, v, &vertex.uv.x, 2);
                        if (color_accessor) cgltf_accessor_read_float(color_accessor, v, &vertex.color.x, 4);
                    }

                    // Populate Layout (always provide full attributes to match shader expectations)
                    if (model.layout.attributes.empty())
                    {
                        model.layout.stride = sizeof(GLTFVertex);
                        model.layout.attributes.push_back({
                            "POSITION0", RHI::FORMAT_R32G32B32A32_SFLOAT, (uint32_t)offsetof(GLTFVertex, pos), 0
                        });
                        model.layout.attributes.push_back({
                            "NORMAL0", RHI::FORMAT_R32G32B32A32_SFLOAT, (uint32_t)offsetof(GLTFVertex, normal), 1
                        });
                        model.layout.attributes.push_back({
                            "TEXCOORD0", RHI::FORMAT_R32G32_SFLOAT, (uint32_t)offsetof(GLTFVertex, uv), 2
                        });
                        model.layout.attributes.push_back({
                            "COLOR0", RHI::FORMAT_R32G32B32A32_SFLOAT, (uint32_t)offsetof(GLTFVertex, color), 3
                        });
                    }

                    GLTFPrimitive gPrim;
                    gPrim.firstIndex = (UInt32)indices.size();
                    gPrim.materialIndex = -1;
                    if (primitive.material)
                    {
                        for (cgltf_size i = 0; i < data->materials_count; ++i)
                        {
                            if (&data->materials[i] == primitive.material)
                            {
                                gPrim.materialIndex = (SInt32)i;
                                break;
                            }
                        }
                    }

                    // Load indices
                    if (primitive.indices)
                    {
                        size_t index_offset = indices.size();
                        size_t index_count = primitive.indices->count;
                        indices.resize(index_offset + index_count);
                        for (size_t idx = 0; idx < index_count; ++idx)
                        {
                            indices[index_offset + idx] = (uint32_t)cgltf_accessor_read_index(primitive.indices, idx) +
                                (uint32_t)vertex_offset;
                        }
                        gPrim.indexCount = (UInt32)index_count;
                    }
                    else
                    {
                        size_t index_offset = indices.size();
                        indices.resize(index_offset + vertex_count);
                        for (size_t idx = 0; idx < vertex_count; ++idx)
                        {
                            indices[index_offset + idx] = (uint32_t)(vertex_offset + idx);
                        }
                        gPrim.indexCount = (UInt32)vertex_count;
                    }
                    model.primitives.push_back(gPrim);
                }
            }

            for (cgltf_size i = 0; i < node->children_count; ++i)
            {
                processNode(node->children[i], worldTransform);
            }
        };

        if (data->scene)
        {
            for (cgltf_size i = 0; i < data->scene->nodes_count; ++i)
            {
                processNode(data->scene->nodes[i], glm::mat4(1.0f));
            }
        }
        else
        {
            // Fallback for files without a scene (unlikely but possible)
            for (cgltf_size i = 0; i < data->nodes_count; ++i)
            {
                if (!data->nodes[i].parent)
                {
                    processNode(&data->nodes[i], glm::mat4(1.0f));
                }
            }
        }


        model.vertexCount = (UInt32)vertices.size();
        model.indexCount = (UInt32)indices.size();

        LOG_INFOF("Loaded GLTF: {0}. Vertices: {1}, Indices: {2}, Primitives: {3}, Materials: {4}",
                  path, vertices.size(), indices.size(), model.primitives.size(), model.materials.size());
        LOG_INFOF("Model Bounds: Min({0},{1},{2}), Max({3},{4},{5})", minBound.x, minBound.y, minBound.z, maxBound.x,
                  maxBound.y, maxBound.z);

        // Create RHI Buffers
        RHI::RHIBufferDescriptor vbDesc{};
        vbDesc.createFlagBits = 0;
        vbDesc.size = sizeof(GLTFVertex) * vertices.size();
        vbDesc.usage = RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT |
            RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT |
            RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR;
        vbDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        vbDesc.queueFamilyIndexCount = 0;
        vbDesc.pQueueFamilyIndices = nullptr;
        vbDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;

        model.vertexBuffer = m_Device->GetFactory()->CreateBuffer(std::move(vbDesc), "GLTF Vertex Buffer");

        RHI::RHIBufferDescriptor ibDesc{};
        ibDesc.createFlagBits = 0;
        ibDesc.size = sizeof(uint32_t) * indices.size();
        ibDesc.usage = RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT |
            RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT | RHI::BUFFER_USAGE_SHADER_DEVICE_ADDRESS_BIT |
            RHI::BUFFER_USAGE_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT_KHR;
        ibDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        ibDesc.queueFamilyIndexCount = 0;
        ibDesc.pQueueFamilyIndices = nullptr;
        ibDesc.memoryUsage = RHI::ERHIMemoryUsage::GpuOnly;

        model.indexBuffer = m_Device->GetFactory()->CreateBuffer(std::move(ibDesc), "GLTF Index Buffer");

        // Upload data using staging buffers
        RHI::RHIBufferDescriptor vsb{};
        vsb.createFlagBits = 0;
        vsb.size = vbDesc.size;
        vsb.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
        vsb.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        vsb.queueFamilyIndexCount = 0;
        vsb.pQueueFamilyIndices = nullptr;
        vsb.memoryUsage = RHI::ERHIMemoryUsage::Upload;

        auto vStaging = m_Device->GetFactory()->CreateBuffer(std::move(vsb), "GLTF Vertex Staging");
        m_Device->GetFactory()->BufferMemoryCopy(vStaging, vertices.data(), vbDesc.size, 0);

        RHI::RHIBufferDescriptor isb{};
        isb.createFlagBits = 0;
        isb.size = ibDesc.size;
        isb.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
        isb.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        isb.queueFamilyIndexCount = 0;
        isb.pQueueFamilyIndices = nullptr;
        isb.memoryUsage = RHI::ERHIMemoryUsage::Upload;

        auto iStaging = m_Device->GetFactory()->CreateBuffer(std::move(isb), "GLTF Index Staging");
        m_Device->GetFactory()->BufferMemoryCopy(iStaging, indices.data(), ibDesc.size, 0);

        auto cmdPool = m_Device->GetFactory()->CreateCommandBufferPool(RHI::RHIQueueType::Graphics);
        auto cmdHandle = m_Device->GetCommandBufferPool(cmdPool)->GetCommandBuffer(0);
        auto cmd = m_Device->GetCommandBuffer(cmdHandle);

        cmd->Begin(RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        cmd->CopyBuffer(vStaging, 0, model.vertexBuffer, 0, vbDesc.size);
        cmd->CopyBuffer(iStaging, 0, model.indexBuffer, 0, ibDesc.size);
        cmd->End();

        m_Device->GetQueue(RHI::RHIQueueType::Graphics)->Submit(cmdHandle);
        m_Device->DeviceWaitIdle();

        m_Device->GetFactory()->ReleaseBuffer(vStaging);
        m_Device->GetFactory()->ReleaseBuffer(iStaging);
        m_Device->GetFactory()->ReleaseCommandBufferPool(cmdPool);
        // Note: cmd is implicitly released when pool is released, or we can release explicitly.
        // But since we release pool immediately, we can skip specific release or do it correctly:
        // m_Device->GetCommandBufferPool(cmdPool)->ReleaseCommandBuffer(0, cmd);

        cgltf_free(data);
        return model;
    }
}
