#include "RHIVkSpirvReflectionService.h"

using namespace ArisenEngine;
#include "Logger/Logger.h"

namespace ArisenEngine::RHI
{
    bool RHIVkSpirvReflectionService::Reflect(const void* spirvCode, size_t size, RHIShaderReflectionData& outData)
    {
        if (!spirvCode || size == 0)
        {
            LOG_ERROR("[RHIVkSpirvReflectionService::Reflect] Invalid SPIR-V code.");
            return false;
        }

        // Check if size is a multiple of 4 (required by SPIRV-Cross / SPIR-V spec)
        if (size % 4 != 0)
        {
            LOG_ERROR("[RHIVkSpirvReflectionService::Reflect] SPIR-V size must be a multiple of 4.");
            return false;
        }

        try
        {
            const uint32_t* codePtr = static_cast<const uint32_t*>(spirvCode);
            std::vector<uint32_t> spirv(codePtr, codePtr + (size / 4));

            spirv_cross::Compiler compiler(std::move(spirv));
            spirv_cross::ShaderResources resources = compiler.get_shader_resources();

            // Set execution model stage
            outData.Stage = static_cast<RHI::ProgramStage>(MapSpirvExecutionModelToStage(compiler.get_execution_model()));

            auto processResources = [&](const spirv_cross::SmallVector<spirv_cross::Resource>& resourceList, EDescriptorType defaultType)
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

                    binding.StageFlags = static_cast<UInt32>(outData.Stage);

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
            // processResources(resources.storage_images, EDescriptorType::DESCRIPTOR_TYPE_STORAGE_IMAGE); 
            // processResources(resources.subpass_inputs, EDescriptorType::DESCRIPTOR_TYPE_INPUT_ATTACHMENT);

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
                range.StageFlags = static_cast<UInt32>(outData.Stage);

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

    EDescriptorType RHIVkSpirvReflectionService::MapSpirvTypeToDescriptorType(const spirv_cross::Compiler& compiler, const spirv_cross::Resource& resource)
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
        case spv::ExecutionModelFragment: return RHI::SHADER_STAGE_FRAGMENT_BIT;
        case spv::ExecutionModelGLCompute: return RHI::SHADER_STAGE_COMPUTE_BIT;
        case spv::ExecutionModelGeometry: return RHI::SHADER_STAGE_GEOMETRY_BIT;
        // ... add others
        default: return 0;
        }
    }
}
