#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Program/DescriptorPool.h"
#include "../../Core/Core.Infra/RHI/Program/RHIDescriptorSet.h"

typedef void* RHI_DeviceHandle;
typedef void* RHI_DescriptorPoolHandle;
typedef void* RHI_DescriptorSetHandle;
typedef void* RHI_PSOHandle;
typedef unsigned long long RHI_BufferHandle;
typedef unsigned long long RHI_ImageHandle;

typedef unsigned long long RHI_ImageViewHandle;

extern "C" ENGINE_DLL RHI_DescriptorPoolHandle RHI_Device_GetDescriptorPool(RHI_DeviceHandle device);
extern "C" ENGINE_DLL unsigned int RHI_DescriptorPool_AddPool(RHI_DescriptorPoolHandle pool, ArisenEngine::Containers::Vector<ArisenEngine::RHI::EDescriptorType>* types, ArisenEngine::Containers::Vector<unsigned int>* counts, unsigned int maxSets);
extern "C" ENGINE_DLL bool RHI_DescriptorPool_Reset(RHI_DescriptorPoolHandle pool, unsigned int poolId);
extern "C" ENGINE_DLL unsigned int RHI_DescriptorPool_AllocDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int layoutIndex, RHI_PSOHandle pso);
extern "C" ENGINE_DLL RHI_DescriptorSetHandle RHI_DescriptorPool_GetDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex);
extern "C" ENGINE_DLL void RHI_DescriptorPool_UpdateDescriptorSets(RHI_DescriptorPoolHandle pool, unsigned int poolId, RHI_PSOHandle pso);
extern "C" ENGINE_DLL void RHI_DescriptorPool_UpdateDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex, RHI_PSOHandle pso);

// Bindless
extern "C" ENGINE_DLL unsigned int RHI_Device_BindlessRegisterImage(RHI_DeviceHandle device, RHI_ImageViewHandle image);
extern "C" ENGINE_DLL unsigned int RHI_Device_BindlessRegisterBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer);


