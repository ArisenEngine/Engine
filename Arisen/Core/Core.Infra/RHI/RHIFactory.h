#include "../Common/CommandHeaders.h"
#include "Program/RHISampler.h"

namespace ArisenEngine::RHI
{
    class Device;
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
        virtual std::shared_ptr<RHISampler> CreateSampler(Device* device, RHISamplerDesc&& desc) = 0;
    };
}
