#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "RHITypesExports.h"

extern "C" ENGINE_DLL RHI_AccelerationStructureHandle RHI_Device_CreateAccelerationStructure(RHI_DeviceHandle device, const char* name);

extern "C" ENGINE_DLL void RHI_Device_GetAccelerationStructureBuildSizes(RHI_DeviceHandle device, const ArisenEngine::RHI::RHIAccelerationStructureBuildGeometryInfo* buildInfo, const unsigned int* pMaxPrimitiveCounts, ArisenEngine::RHI::RHIAccelerationStructureBuildSizesInfo* pSizeInfo);
extern "C" ENGINE_DLL bool RHI_Device_AllocAccelerationStructure(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle, unsigned int type, unsigned long long size, RHI_BufferHandle buffer, unsigned long long offset);
extern "C" ENGINE_DLL void RHI_Device_ReleaseAccelerationStructure(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle);
extern "C" ENGINE_DLL unsigned long long RHI_Device_GetAccelerationStructureDeviceAddress(RHI_DeviceHandle device, RHI_AccelerationStructureHandle handle);

extern "C" ENGINE_DLL void RHI_Device_GetRayTracingShaderGroupHandles(RHI_DeviceHandle device, RHI_PipelineHandle pipeline, unsigned int firstGroup, unsigned int groupCount, unsigned long long size, void* pData);
