#pragma once
namespace ArisenEngine::RHI
{
    typedef enum EImageTiling
    {
        IMAGE_TILING_OPTIMAL = 0,
        IMAGE_TILING_LINEAR = 1,
        IMAGE_TILING_DRM_FORMAT_MODIFIER_EXT = 1000158000,
        IMAGE_TILING_MAX_ENUM = 0x7FFFFFFF
    } EImageTiling;
}
