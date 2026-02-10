#include "RayTracingExports.h"
#include "RHINativeBridge.h"

using namespace ArisenEngine::RHI;

extern "C" void RHI_Device_GetAccelerationStructureBuildSizes(RHI_DeviceHandle device, const RHIAccelerationStructureBuildGeometryInfo* buildInfo, const unsigned int* pMaxPrimitiveCounts, RHIAccelerationStructureBuildSizesInfo* pSizeInfo)
{
    if (!device) return;
    static_cast<RHIDevice*>(device)->GetAccelerationStructureBuildSizes(*buildInfo, pMaxPrimitiveCounts, pSizeInfo);
}

extern "C" bool RHI_Device_AllocAccelerationStructure(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle, unsigned int type, unsigned long long size, RHI_BufferHandle buffer, unsigned long long offset)
{
    if (!device) return false;
    return static_cast<RHIDevice*>(device)->AllocAccelerationStructure(handle, (ERHIAccelerationStructureType)type, size, buffer, offset);
}

extern "C" void RHI_Device_ReleaseAccelerationStructure(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle)
{
    if (!device) return;
    static_cast<RHIDevice*>(device)->ReleaseAccelerationStructure(handle);
}

extern "C" unsigned long long RHI_Device_GetAccelerationStructureDeviceAddress(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle)
{
    if (!device) return 0;
    return static_cast<RHIDevice*>(device)->GetAccelerationStructureDeviceAddress(handle);
}

extern "C" void RHI_Device_GetRayTracingShaderGroupHandles(RHI_DeviceHandle device, RHI_PipelineHandle pipeline, unsigned int firstGroup, unsigned int groupCount, unsigned long long size, void* pData)
{
    if (!device) return;
    static_cast<RHIDevice*>(device)->GetRayTracingShaderGroupHandles(pipeline, firstGroup, groupCount, size, pData);
}
