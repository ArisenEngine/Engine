#include "../Common/CommandHeaders.h"
#include "Program/RHISampler.h"

namespace ArisenEngine::RHI
{
    class RHIDevice;
    class RHISampler;
}

namespace ArisenEngine::RHI
{
    class RHIFactory
    {
    public:
        RHIFactory() = default;
        NO_COPY_NO_MOVE(RHIFactory)
        VIRTUAL_DECONSTRUCTOR(RHIFactory)
        virtual RHISampler* CreateSampler(RHIDevice* device, RHISamplerDesc&& desc) = 0;
    };
}
