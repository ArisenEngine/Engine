#pragma once
#include "Common/CommandHeaders.h"

namespace NebulaEngine::Graphics
{
    class RHISelector
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHISelector)

        void SetCurrentGraphicsAPI(RHI::GraphsicsAPI api_type);

        void Dispose();
    private:

        RHI::GraphsicsAPI _api_type = RHI::GraphsicsAPI::None;
        HMODULE _rhi_dll = NULL;
    };
}
