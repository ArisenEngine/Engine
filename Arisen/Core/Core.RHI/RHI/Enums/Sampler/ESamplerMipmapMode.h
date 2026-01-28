#pragma once
namespace ArisenEngine::RHI
{
    typedef enum ESamplerMipmapMode {
        SAMPLER_MIPMAP_MODE_NEAREST = 0,
        SAMPLER_MIPMAP_MODE_LINEAR = 1,
        SAMPLER_MIPMAP_MODE_MAX_ENUM = 0x7FFFFFFF
    } ESamplerMipmapMode;
}
