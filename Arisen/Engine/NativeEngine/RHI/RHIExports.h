#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/RHIInstance.h"
#include "../../Core/Core.Infra/RHI/GraphsicsAPI.h"

extern "C" ENGINE_DLL void RHI_SetGraphicsAPI(ArisenEngine::RHI::GraphsicsAPI api);

// Opaque handle types for C ABI
typedef void* RHI_InstanceHandle;
typedef void* RHI_DeviceHandle;
typedef void* RHI_FactoryHandle;

// Keep only API selector here; detailed per-class exports live in dedicated files


