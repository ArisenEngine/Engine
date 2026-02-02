#define GLM_ENABLE_EXPERIMENTAL
#define CGLTF_IMPLEMENTATION
#include <cgltf.h>
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
                    }
                    else
                    {
                        size_t index_offset = indices.size();
                        indices.resize(index_offset + vertex_count);
                        for (size_t idx = 0; idx < vertex_count; ++idx)
                        {
                            indices[index_offset + idx] = (uint32_t)(vertex_offset + idx);
                        }
                    }
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
        
        LOG_INFOF("Loaded GLTF: {0}. Vertices: {1}, Indices: {2}", path, vertices.size(), indices.size());

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
