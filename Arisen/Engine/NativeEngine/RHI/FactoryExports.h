#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Program/RHISampler.h"

typedef void* RHI_DeviceHandle;
typedef unsigned long long RHI_SamplerHandle;

// Factory layer removed: creation lives on RHIDevice (see DeviceExports).
extern "C" ENGINE_DLL void RHI_Sampler_Destroy(RHI_DeviceHandle device, RHI_SamplerHandle sampler);


