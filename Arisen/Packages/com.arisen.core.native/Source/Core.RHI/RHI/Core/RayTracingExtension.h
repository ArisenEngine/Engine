#pragma once
#include "Base/FoundationMinimal.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildSizesInfo;

    /**
     * @brief Interface for Ray Tracing specific operations.
     */
    class RayTracingExtension
    {
    public:
        virtual ~RayTracingExtension() = default;

        virtual void GetAccelerationStructureBuildSizes(
            const RHIAccelerationStructureBuildGeometryInfo& buildInfo,
            const UInt32* pMaxPrimitiveCounts,
            RHIAccelerationStructureBuildSizesInfo* pSizeInfo) = 0;

        virtual UInt64 GetAccelerationStructureDeviceAddress(RHIAccelerationStructureHandle handle) = 0;

        virtual void GetRayTracingShaderGroupHandles(
            RHIPipelineHandle pipeline,
            UInt32 firstGroup,
            UInt32 groupCount,
            UInt64 size,
            void* pData) = 0;
    };
}
