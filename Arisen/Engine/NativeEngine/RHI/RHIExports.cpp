#include "RHIExports.h"
#include "RHILoader.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_SetGraphicsAPI(RHI::GraphsicsAPI api)
{
    Graphics::RHILoader::SetCurrentGraphicsAPI(api);
}
// Intentionally left minimal: only API selection lives here.


