#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/RHIFactory.h"
#include "../../Core/Core.Infra/RHI/Program/RHISampler.h"

typedef void* RHI_FactoryHandle;
typedef void* RHI_DeviceHandle;
typedef void* RHI_SamplerHandle;

extern "C" ENGINE_DLL RHI_SamplerHandle RHI_Factory_CreateSampler(RHI_FactoryHandle factory, RHI_DeviceHandle device, const ArisenEngine::RHI::RHISamplerDesc* desc);
extern "C" ENGINE_DLL void RHI_Factory_Destroy(RHI_FactoryHandle factory);
extern "C" ENGINE_DLL void RHI_Sampler_Destroy(RHI_SamplerHandle sampler);


