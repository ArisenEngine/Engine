#include "Services/RHIVkSpirvReflectionService.h"

using namespace ArisenEngine;
#include "Logger/Logger.h"

namespace ArisenEngine::RHI
{
    bool RHIVkSpirvReflectionService::Reflect(const void* spirvCode, size_t size, RHIShaderReflectionData& outData)
    {
        return ReflectEntryPoint(spirvCode, size, nullptr, outData);
    }

    bool RHIVkSpirvReflectionService::ReflectEntryPoint(const void* spirvCode, size_t size, const char* entryPoint,
                                                        RHIShaderReflectionData& outData)
    {
        if (!spirvCode || size == 0)
        {
            LOG_ERROR("[RHIVkSpirvReflectionService::Reflect] Invalid SPIR-V code.");
            return false;
        }

        // Check if size is a multiple of 4 (required by SPIRV-Cross / SPIR-V spec)
        if (size % 4 != 0)
        {
            LOG_ERROR("[RHIVkSpirvReflectionService::ReflectEntryPoint] Invalid SPIR-V code.");
            return false;
        }

        // Check if size is a multiple of 4 (required by SPIRV-Cross / SPIR-V spec)
        if (size % 4 != 0)
        {
            LOG_ERROR("[RHIVkSpirvReflectionService::ReflectEntryPoint] SPIR-V size must be a multiple of 4.");
            return false;
        }

        try
        {
            const uint32_t* codePtr = static_cast<const uint32_t*>(spirvCode);
            std::vector<uint32_t> spirv(codePtr, codePtr + (size / 4));

            spirv_cross::Compiler compiler(std::move(spirv));

            if (entryPoint && *entryPoint)
            {
                auto entryPoints = compiler.get_entry_points_and_stages();
                bool found = false;
                for (const auto& ep : entryPoints)
                {
                    if (ep.name == entryPoint)
                    {
                        compiler.set_entry_point(entryPoint, ep.execution_model);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    LOG_WARN(
                        String::Format(
                            "[RHIVkSpirvReflectionService::ReflectEntryPoint] Entry point '%s' not found in SPIR-V.",
                            entryPoint));
                }
            }

            spirv_cross::ShaderResources resources = compiler.get_shader_resources();

            // Set execution model stage
            spv::ExecutionModel model = compiler.get_execution_model();
            outData.Stage = MapSpirvExecutionModelToProgramStage(model);
            UInt32 stageBit = MapSpirvExecutionModelToStage(model);

            auto processResources = [&](const spirv_cross::SmallVector<spirv_cross::Resource>& resourceList,
                                        EDescriptorType defaultType)
            {
                for (const auto& res : resourceList)
                {
                    RHIShaderResourceBinding binding{};
                    binding.Name = res.name;
                    binding.Set = compiler.get_decoration(res.id, spv::DecorationDescriptorSet);
                    binding.Binding = compiler.get_decoration(res.id, spv::DecorationBinding);

                    const auto& type = compiler.get_type(res.type_id);
                    // Handle array size
                    if (!type.array.empty())
                    {
                        // For now support 1D array
                        binding.Count = type.array[0];
                    }
                    else
                    {
                        binding.Count = 1;
                    }

                    binding.StageFlags = stageBit;

                    // Determine descriptor type dynamically or use default
                    if (defaultType == EDescriptorType::DESCRIPTOR_TYPE_MAX_ENUM)
                    {
                        binding.DescriptorType = MapSpirvTypeToDescriptorType(compiler, res);
                    }
                    else
                    {
                        binding.DescriptorType = defaultType;
                    }

                    outData.ResourceBindings.push_back(binding);
                }
            };

            // Process different resource types
            processResources(resources.uniform_buffers, EDescriptorType::DESCRIPTOR_TYPE_UNIFORM_BUFFER);
            processResources(resources.storage_buffers, EDescriptorType::DESCRIPTOR_TYPE_STORAGE_BUFFER);
            processResources(resources.separate_images, EDescriptorType::DESCRIPTOR_TYPE_SAMPLED_IMAGE);
            processResources(resources.separate_samplers, EDescriptorType::DESCRIPTOR_TYPE_SAMPLER);
            processResources(resources.sampled_images, EDescriptorType::DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER);
            processResources(resources.storage_images, EDescriptorType::DESCRIPTOR_TYPE_STORAGE_IMAGE);
            processResources(resources.subpass_inputs, EDescriptorType::DESCRIPTOR_TYPE_INPUT_ATTACHMENT);
            processResources(resources.acceleration_structures,
                             EDescriptorType::DESCRIPTOR_TYPE_ACCELERATION_STRUCTURE_KHR);

            // Push Constants
            for (const auto& res : resources.push_constant_buffers)
            {
                const auto& type = compiler.get_type(res.type_id);
                // Get the struct size
                size_t structSize = compiler.get_declared_struct_size(type);

                RHIPushConstantRange range{};
                range.Name = res.name;
                range.Offset = 0; // Usually 0 for the block, unless manually offset
                range.Size = static_cast<UInt32>(structSize);
                range.StageFlags = stageBit;

                outData.PushConstants.push_back(range);
            }
        }
        catch (const std::exception& e)
        {
            LOG_ERROR(String::Format("[RHIVkSpirvReflectionService::Reflect] SPIRV-Cross exception: %s", e.what()));
            return false;
        }

        return true;
    }

    EDescriptorType RHIVkSpirvReflectionService::MapSpirvTypeToDescriptorType(
        const spirv_cross::Compiler& compiler, const spirv_cross::Resource& resource)
    {
        // Fallback or complex logic if needed. 
        // For now, most types are passed explicitly in processResources.
        return EDescriptorType::DESCRIPTOR_TYPE_UNIFORM_BUFFER;
    }

    UInt32 RHIVkSpirvReflectionService::MapSpirvExecutionModelToStage(spv::ExecutionModel model)
    {
        switch (model)
        {
        case spv::ExecutionModelVertex: return RHI::SHADER_STAGE_VERTEX_BIT;
        case spv::ExecutionModelTessellationControl: return RHI::SHADER_STAGE_TESSELLATION_CONTROL_BIT;
        case spv::ExecutionModelTessellationEvaluation: return RHI::SHADER_STAGE_TESSELLATION_EVALUATION_BIT;
        case spv::ExecutionModelGeometry: return RHI::SHADER_STAGE_GEOMETRY_BIT;
        case spv::ExecutionModelFragment: return RHI::SHADER_STAGE_FRAGMENT_BIT;
        case spv::ExecutionModelGLCompute: return RHI::SHADER_STAGE_COMPUTE_BIT;
        case spv::ExecutionModelTaskEXT: return RHI::SHADER_STAGE_TASK_BIT_EXT;
        case spv::ExecutionModelMeshEXT: return RHI::SHADER_STAGE_MESH_BIT_EXT;
        case spv::ExecutionModelTaskNV: return RHI::SHADER_STAGE_TASK_BIT_NV;
        case spv::ExecutionModelMeshNV: return RHI::SHADER_STAGE_MESH_BIT_NV;
        case spv::ExecutionModelRayGenerationKHR: return RHI::SHADER_STAGE_RAYGEN_BIT;
        case spv::ExecutionModelAnyHitKHR: return RHI::SHADER_STAGE_ANY_HIT_BIT;
        case spv::ExecutionModelClosestHitKHR: return RHI::SHADER_STAGE_CLOSEST_HIT_BIT;
        case spv::ExecutionModelMissKHR: return RHI::SHADER_STAGE_MISS_BIT;
        case spv::ExecutionModelIntersectionKHR: return RHI::SHADER_STAGE_INTERSECTION_BIT;
        case spv::ExecutionModelCallableKHR: return RHI::SHADER_STAGE_CALLABLE_BIT;
        default: return 0;
        }
    }

    EProgramStage RHIVkSpirvReflectionService::MapSpirvExecutionModelToProgramStage(spv::ExecutionModel model)
    {
        switch (model)
        {
        case spv::ExecutionModelVertex: return EProgramStage::Vertex;
        case spv::ExecutionModelTessellationControl: return EProgramStage::Hull;
        case spv::ExecutionModelTessellationEvaluation: return EProgramStage::Domain;
        case spv::ExecutionModelGeometry: return EProgramStage::Geometry;
        case spv::ExecutionModelFragment: return EProgramStage::Fragment;
        case spv::ExecutionModelGLCompute: return EProgramStage::Compute;
        case spv::ExecutionModelTaskEXT:
        case spv::ExecutionModelTaskNV: return EProgramStage::Amplification;
        case spv::ExecutionModelMeshEXT:
        case spv::ExecutionModelMeshNV: return EProgramStage::Mesh;
        case spv::ExecutionModelRayGenerationKHR:
        case spv::ExecutionModelAnyHitKHR:
        case spv::ExecutionModelClosestHitKHR:
        case spv::ExecutionModelMissKHR:
        case spv::ExecutionModelIntersectionKHR:
        case spv::ExecutionModelCallableKHR: return EProgramStage::RayTracing;
        default: return EProgramStage::STAGE_MAX;
        }
    }
}
