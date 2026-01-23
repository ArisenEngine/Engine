#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/RHIInstance.h"
#include "../../Core/Core.Infra/RHI/GraphsicsAPI.h"

extern "C" ENGINE_DLL void RHI_SetGraphicsAPI(ArisenEngine::RHI::GraphicsAPI api);

#include "RHIHandleExports.h"

// Keep only API selector here; detailed per-class exports live in dedicated files


