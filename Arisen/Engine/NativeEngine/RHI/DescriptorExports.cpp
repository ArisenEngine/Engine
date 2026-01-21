#include "DescriptorExports.h"
#include "HandlesExports.h"
#include "../../Core/Core.Infra/RHI/Devices/RHIDevice.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_DescriptorPoolHandle RHI_Device_GetDescriptorPool(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    if (dev == nullptr) return nullptr;
    return reinterpret_cast<RHI_DescriptorPoolHandle>(dev->GetDescriptorPool());
}

extern "C" ENGINE_DLL unsigned int RHI_DescriptorPool_AddPool(RHI_DescriptorPoolHandle pool, Containers::Vector<RHI::EDescriptorType>* types, Containers::Vector<unsigned int>* counts, unsigned int maxSets)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    if (p == nullptr || types == nullptr || counts == nullptr) return 0U;
    return p->AddPool(*types, *counts, maxSets);
}

extern "C" ENGINE_DLL bool RHI_DescriptorPool_Reset(RHI_DescriptorPoolHandle pool, unsigned int poolId)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    if (p == nullptr) return false;
    return p->ResetPool(poolId);
}

extern "C" ENGINE_DLL unsigned int RHI_DescriptorPool_AllocDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int layoutIndex, RHI_PSOHandle pso)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (p == nullptr || s == nullptr) return 0U;
    return p->AllocDescriptorSet(poolId, layoutIndex, s);
}

extern "C" ENGINE_DLL RHI_DescriptorSetHandle RHI_DescriptorPool_GetDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    if (p == nullptr) return nullptr;
    return reinterpret_cast<RHI_DescriptorSetHandle>(p->GetDescriptorSet(poolId, setIndex));
}

extern "C" ENGINE_DLL void RHI_DescriptorPool_UpdateDescriptorSets(RHI_DescriptorPoolHandle pool, unsigned int poolId, RHI_PSOHandle pso)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (p == nullptr || s == nullptr) return;
    p->UpdateDescriptorSets(poolId, s);
}

extern "C" ENGINE_DLL void RHI_DescriptorPool_UpdateDescriptorSet(RHI_DescriptorPoolHandle pool, unsigned int poolId, unsigned int setIndex, RHI_PSOHandle pso)
{
    auto* p = reinterpret_cast<RHI::DescriptorPool*>(pool);
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (p == nullptr || s == nullptr) return;
    p->UpdateDescriptorSet(poolId, setIndex, s);
}

extern "C" ENGINE_DLL unsigned int RHI_Device_BindlessRegisterImage(RHI_DeviceHandle device, RHI_ImageHandle image)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    auto* img = reinterpret_cast<RHI::ImageHandle*>(image);
    if (dev == nullptr || img == nullptr) return 0xFFFFFFFF;
    return dev->RegisterBindlessResource(img);
}

extern "C" ENGINE_DLL unsigned int RHI_Device_BindlessRegisterBuffer(RHI_DeviceHandle device, RHI_BufferHandle buffer)
{
    auto* dev = reinterpret_cast<RHI::RHIDevice*>(device);
    auto* buf = reinterpret_cast<RHI::BufferHandle*>(buffer);
    if (dev == nullptr || buf == nullptr) return 0xFFFFFFFF;
    return dev->RegisterBindlessResource(buf);
}


