#pragma once
#include "../Definitions/RHICommon.h"
#include "../Enums/Pipeline/EDescriptorType.h"
#include "../Enums/Pipeline/ProgramStage.h"
#include <vector>
#include <string>

namespace ArisenEngine::RHI
{
    // Represents a single resource binding (e.g., Uniform Buffer, Texture)
    struct RHIShaderResourceBinding
    {
        String Name;
        UInt32 Set;
        UInt32 Binding;
        UInt32 Count; // Array size (1 for non-arrays)
        EDescriptorType DescriptorType;
        UInt32 StageFlags; // ProgramStage bits

        bool operator==(const RHIShaderResourceBinding& other) const
        {
            return Set == other.Set && Binding == other.Binding;
        }
    };

    // Represents a Push Constant range
    struct RHIPushConstantRange
    {
        String Name;
        UInt32 Offset;
        UInt32 Size;
        UInt32 StageFlags;
    };

    // Reflection data for a single shader stage
    struct RHIShaderReflectionData
    {
        RHI::ProgramStage Stage;
        std::vector<RHIShaderResourceBinding> ResourceBindings;
        std::vector<RHIPushConstantRange> PushConstants;
    };

    // Interface for reflection services
    class IRHIShaderReflection
    {
    public:
        virtual ~IRHIShaderReflection() = default;

        // Reflects a SPIR-V binary and populates the output data
        virtual bool Reflect(const void* spirvCode, size_t size, RHIShaderReflectionData& outData) = 0;
    };
}
