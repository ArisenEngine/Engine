#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/RayTracing/ERHIAccelerationStructureType.h"
#include "RHI/Enums/RayTracing/ERHIAccelerationStructureBuildFlag.h"
#include "RHI/Enums/RayTracing/ERHIAccelerationStructureGeometryType.h"
#include "RHI/Enums/RayTracing/ERHIAccelerationStructureGeometryFlag.h"
#include "RHI/Enums/RayTracing/ERHIAccelerationStructureInstanceFlag.h"
#include "RHI/Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    struct RHIAccelerationStructureGeometryTrianglesData
    {
        EFormat vertexFormat;
        UInt64 vertexData; // Device Address
        UInt64 vertexStride;
        UInt32 maxVertex;
        EIndexType indexType;
        UInt64 indexData; // Device Address
        UInt64 transformData; // Device Address (Optional)
    };

    struct RHIAccelerationStructureGeometryAabbsData
    {
        UInt64 data; // Device Address
        UInt64 stride;
    };

    struct RHIAccelerationStructureGeometryInstancesData
    {
        bool arrayOfPointers;
        UInt64 data; // Device Address
    };

    struct RHIAccelerationStructureGeometryData
    {
        ERHIAccelerationStructureGeometryType type;
        ERHIAccelerationStructureGeometryFlags flags;
        union
        {
            RHIAccelerationStructureGeometryTrianglesData triangles;
            RHIAccelerationStructureGeometryAabbsData aabbs;
            RHIAccelerationStructureGeometryInstancesData instances;
        };
    };

    struct RHIAccelerationStructureBuildRangeInfo
    {
        UInt32 primitiveCount;
        UInt32 primitiveOffset;
        UInt32 firstVertex;
        UInt32 transformOffset;
    };

    struct RHIAccelerationStructureBuildGeometryInfo
    {
        ERHIAccelerationStructureType type;
        ERHIAccelerationStructureBuildFlags flags;
        UInt32 geometryCount;
        const RHIAccelerationStructureGeometryData* pGeometries;
        RHIAccelerationStructureHandle dstAccelerationStructure;
        RHIAccelerationStructureHandle srcAccelerationStructure;
        RHIBufferHandle scratchData;
    };

    struct RHIAccelerationStructureBuildSizesInfo
    {
        UInt64 accelerationStructureSize;
        UInt64 updateScratchSize;
        UInt64 buildScratchSize;
    };

    class RHIAccelerationStructure
    {
    public:
        virtual ~RHIAccelerationStructure() = default;
        virtual void* GetHandle() const = 0;
        virtual UInt64 GetDeviceAddress() const = 0;
    };
}
