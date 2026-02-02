#define GLM_ENABLE_EXPERIMENTAL
#define CGLTF_IMPLEMENTATION
#include <cgltf.h>
#include "stb_image.h"
#include "RHITestBase.h"
#include <iostream>
#include <filesystem>
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/SyncExports.h"
#include "RHI/Enums/Buffer/EBufferUsage.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Enums/Pipeline/ECommandBufferUsageFlagBits.h"
#include <functional>
#include <glm/gtc/type_ptr.hpp>
#include <glm/gtx/quaternion.hpp>


using namespace ArisenEngine;

namespace ArisenEngine::Testing
{
    GLTFModel RHITestBase::LoadGLTF(const std::string& path)
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

        GLTFModel model;

        std::filesystem::path modelPath(path);
        std::filesystem::path modelDir = modelPath.parent_path();

        // Helper for image uploading and mipmap generation
        auto uploadAndMipmap = [&](RHI_ImageHandle texture, UInt32 width, UInt32 height, void* data, UInt32 mipLevels) {
            RHI::RHIBufferDescriptor tsb{
                0, (UInt64)width * height * 4, RHI::BUFFER_USAGE_TRANSFER_SRC_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr, RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT
            };
            auto stagingBuffer = RHI_Device_CreateBuffer(m_Device, &tsb, "Texture Staging Buffer");
            RHI_Buffer_MemoryCopy(m_Device, stagingBuffer, data, 0);

            auto cmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
            auto cmd = RHI_Device_GetCommandBuffer(m_Device, cmdPool, 0);
            RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
            
            // Transition to DST
            {
                RHI::RHIImageMemoryBarrier barrier{};
                barrier.srcAccess = RHI::ACCESS_NONE;
                barrier.dstAccess = RHI::ACCESS_TRANSFER_WRITE_BIT;
                barrier.oldLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
                barrier.newLayout = RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
                barrier.image = *reinterpret_cast<RHI::RHIImageHandle*>(&texture);
                barrier.subresourceRange = { RHI::IMAGE_ASPECT_COLOR_BIT, 0, mipLevels, 0, 1 };
                barrier.srcStageMask = RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT;
                barrier.dstStageMask = RHI::PIPELINE_STAGE_TRANSFER_BIT;

                Containers::Vector<RHI::RHIImageMemoryBarrier> barriers { barrier };
                RHI_Cmd_PipelineBarrier_Image(cmd, RHI::PIPELINE_STAGE_TOP_OF_PIPE_BIT, RHI::PIPELINE_STAGE_TRANSFER_BIT, 0, &barriers);
            }

            // Copy
            {
                Containers::Vector<RHI::RHIBufferImageCopy> regions{
                    { 0, 0, 0, { RHI::IMAGE_ASPECT_COLOR_BIT, 0, 0, 1 }, 0, 0, 0, width, height, 1 }
                };
                RHI_Cmd_CopyBufferToImage(cmd, stagingBuffer, texture, RHI::IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, &regions);
            }

            // Generate Mipmaps
            RHI_Cmd_GenerateMipmaps(cmd, texture);

            RHI_Cmd_End(cmd);
            RHI_Device_Submit(m_Device, cmd, 0);
            RHI_Device_WaitIdle(m_Device);

            RHI_Device_ReleaseBuffer(m_Device, stagingBuffer);
            RHI_Device_ReleaseCommandBuffer(m_Device, cmdPool, 0, cmd);
            RHI_Device_ReleaseCommandBufferPool(m_Device, cmdPool);
        };

        // Load Materials
        for (cgltf_size i = 0; i < data->materials_count; ++i)
        {
            cgltf_material& mat = data->materials[i];
            GLTFMaterial gMat;
            
            if (mat.has_pbr_metallic_roughness && mat.pbr_metallic_roughness.base_color_texture.texture)
            {
                cgltf_texture* tex = mat.pbr_metallic_roughness.base_color_texture.texture;
                if (tex->image && tex->image->uri)
                {
                    auto texPath = (modelDir / tex->image->uri).string();
                    int tw, th, tc;
                    stbi_uc* pixels = stbi_load(texPath.c_str(), &tw, &th, &tc, STBI_rgb_alpha);
                    if (pixels)
                    {
                        RHI::RHIImageDescriptor texDesc = {};
                        texDesc.imageType = RHI::IMAGE_TYPE_2D;
                        texDesc.width = (UInt32)tw;
                        texDesc.height = (UInt32)th;
                        texDesc.depth = 1;
                        texDesc.mipLevels = static_cast<uint32_t>(std::floor(std::log2(std::max(tw, th)))) + 1;
                        texDesc.arrayLayers = 1;
                        texDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                        texDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
                        texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT | RHI::IMAGE_USAGE_SAMPLED_BIT;
                        texDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
                        texDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
                        gMat.baseColorTexture = RHI_Device_CreateImage(m_Device, &texDesc, tex->image->uri);

                        RHI::RHIImageViewDesc viewDesc = {};
                        viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                        viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                        viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                        viewDesc.levelCount = texDesc.mipLevels;
                        viewDesc.layerCount = 1;
                        gMat.baseColorView = RHI_Image_AddImageView(m_Device, gMat.baseColorTexture, &viewDesc);

                        uploadAndMipmap(gMat.baseColorTexture, tw, th, pixels, texDesc.mipLevels);
                        stbi_image_free(pixels);
                    }
                    else
                    {
                        LOG_ERRORF("Failed to load texture: {0}", texPath);
                    }
                }
            }

            // Create Fallback if loading failed
            if (!gMat.baseColorTexture)
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
                texDesc.usage = RHI::IMAGE_USAGE_TRANSFER_SRC_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT | RHI::IMAGE_USAGE_SAMPLED_BIT;
                texDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
                texDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
                gMat.baseColorTexture = RHI_Device_CreateImage(m_Device, &texDesc, "Fallback White");

                RHI::RHIImageViewDesc viewDesc = {};
                viewDesc.viewType = RHI::IMAGE_VIEW_TYPE_2D;
                viewDesc.format = RHI::FORMAT_R8G8B8A8_SRGB;
                viewDesc.aspectMask = RHI::IMAGE_ASPECT_COLOR_BIT;
                viewDesc.levelCount = 1;
                viewDesc.layerCount = 1;
                gMat.baseColorView = RHI_Image_AddImageView(m_Device, gMat.baseColorTexture, &viewDesc);
                
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
            gMat.sampler = RHI_Device_CreateSampler(m_Device, &sampDesc);

            model.materials.push_back(gMat);
        }

        // Helper to traverse nodes and apply transforms
        std::function<void(cgltf_node*, const glm::mat4&)> processNode;
        processNode = [&](cgltf_node* node, const glm::mat4& parentTransform) {
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
                        vertex.pos = glm::vec3(worldTransform * glm::vec4(localPos, 1.0f));

                        if (normal_accessor) {
                            glm::vec3 localNormal;
                            cgltf_accessor_read_float(normal_accessor, v, &localNormal.x, 3);
                            vertex.normal = glm::normalize(glm::vec3(worldTransform * glm::vec4(localNormal, 0.0f)));
                        }
                        if (uv_accessor) cgltf_accessor_read_float(uv_accessor, v, &vertex.uv.x, 2);
                        if (color_accessor) cgltf_accessor_read_float(color_accessor, v, &vertex.color.x, 4);
                    }

                    // Populate Layout (always provide full attributes to match shader expectations)
                    if (model.layout.attributes.empty())
                    {
                        model.layout.stride = sizeof(GLTFVertex);
                        model.layout.attributes.push_back({"POSITION0", RHI::FORMAT_R32G32B32_SFLOAT, (uint32_t)offsetof(GLTFVertex, pos), 0});
                        model.layout.attributes.push_back({"NORMAL0", RHI::FORMAT_R32G32B32_SFLOAT, (uint32_t)offsetof(GLTFVertex, normal), 1});
                        model.layout.attributes.push_back({"TEXCOORD0", RHI::FORMAT_R32G32_SFLOAT, (uint32_t)offsetof(GLTFVertex, uv), 2});
                        model.layout.attributes.push_back({"COLOR0", RHI::FORMAT_R32G32B32A32_SFLOAT, (uint32_t)offsetof(GLTFVertex, color), 3});
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
                            indices[index_offset + idx] = (uint32_t)cgltf_accessor_read_index(primitive.indices, idx) + (uint32_t)vertex_offset;
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


        model.indexCount = (UInt32)indices.size();
        
        LOG_INFOF("Loaded GLTF: {0}. Vertices: {1}, Indices: {2}, Primitives: {3}, Materials: {4}", 
            path, vertices.size(), indices.size(), model.primitives.size(), model.materials.size());

        // Create RHI Buffers
        RHI::RHIBufferDescriptor vbDesc{};
        vbDesc.createFlagBits = 0;
        vbDesc.size = sizeof(GLTFVertex) * vertices.size();
        vbDesc.usage = RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT;
        vbDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        vbDesc.queueFamilyIndexCount = 0;
        vbDesc.pQueueFamilyIndices = nullptr;
        vbDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
        
        model.vertexBuffer = RHI_Device_CreateBuffer(m_Device, &vbDesc, "GLTF Vertex Buffer");

        RHI::RHIBufferDescriptor ibDesc{};
        ibDesc.createFlagBits = 0;
        ibDesc.size = sizeof(uint32_t) * indices.size();
        ibDesc.usage = RHI::BUFFER_USAGE_TRANSFER_DST_BIT | RHI::BUFFER_USAGE_INDEX_BUFFER_BIT;
        ibDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        ibDesc.queueFamilyIndexCount = 0;
        ibDesc.pQueueFamilyIndices = nullptr;
        ibDesc.memoryPropertyFlags = RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT;
        
        model.indexBuffer = RHI_Device_CreateBuffer(m_Device, &ibDesc, "GLTF Index Buffer");

        // Upload data using staging buffers
        RHI::RHIBufferDescriptor vsb{};
        vsb.createFlagBits = 0;
        vsb.size = vbDesc.size;
        vsb.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
        vsb.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        vsb.queueFamilyIndexCount = 0;
        vsb.pQueueFamilyIndices = nullptr;
        vsb.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
        
        auto vStaging = RHI_Device_CreateBuffer(m_Device, &vsb, "GLTF Vertex Staging");
        RHI_Buffer_MemoryCopy(m_Device, vStaging, vertices.data(), 0);

        RHI::RHIBufferDescriptor isb{};
        isb.createFlagBits = 0;
        isb.size = ibDesc.size;
        isb.usage = RHI::BUFFER_USAGE_TRANSFER_SRC_BIT;
        isb.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;
        isb.queueFamilyIndexCount = 0;
        isb.pQueueFamilyIndices = nullptr;
        isb.memoryPropertyFlags = RHI::MEMORY_PROPERTY_HOST_VISIBLE_BIT | RHI::MEMORY_PROPERTY_HOST_COHERENT_BIT;
        
        auto iStaging = RHI_Device_CreateBuffer(m_Device, &isb, "GLTF Index Staging");
        RHI_Buffer_MemoryCopy(m_Device, iStaging, indices.data(), 0);

        auto cmdPool = RHI_Device_CreateCommandBufferPool(m_Device);
        auto cmd = RHI_Device_GetCommandBuffer(m_Device, cmdPool, 0);
        RHI_Cmd_Begin(cmd, 0, RHI::COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT);
        RHI_Cmd_CopyBuffer(cmd, vStaging, 0, model.vertexBuffer, 0, vbDesc.size);
        RHI_Cmd_CopyBuffer(cmd, iStaging, 0, model.indexBuffer, 0, ibDesc.size);
        RHI_Cmd_End(cmd);
        
        RHI_Device_Submit(m_Device, cmd, 0);
        RHI_Device_WaitIdle(m_Device);

        RHI_Device_ReleaseBuffer(m_Device, vStaging);
        RHI_Device_ReleaseBuffer(m_Device, iStaging);
        RHI_Device_ReleaseCommandBuffer(m_Device, cmdPool, 0, cmd);
        RHI_Device_ReleaseCommandBufferPool(m_Device, cmdPool);

        cgltf_free(data);
        return model;
    }
}
