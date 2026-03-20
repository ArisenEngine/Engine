#pragma once
#include "RHI/Pipeline/RHIShaderReflection.h"
#include <spirv_cross/spirv_cross.hpp>
#include <spirv_cross/spirv_glsl.hpp>

namespace ArisenEngine::RHI
{
    class RHIVkSpirvReflectionService : public IRHIShaderReflection
    {
    public:
        RHIVkSpirvReflectionService() = default;
        ~RHIVkSpirvReflectionService() override = default;

        bool Reflect(const void* spirvCode, size_t size, RHIShaderReflectionData& outData) override;
        bool ReflectEntryPoint(const void* spirvCode, size_t size, const char* entryPoint,
                               RHIShaderReflectionData& outData);

    private:
        EDescriptorType MapSpirvTypeToDescriptorType(const spirv_cross::Compiler& compiler,
                                                     const spirv_cross::Resource& resource);
        UInt32 MapSpirvExecutionModelToStage(spv::ExecutionModel model);
        EProgramStage MapSpirvExecutionModelToProgramStage(spv::ExecutionModel model);
    };
}
