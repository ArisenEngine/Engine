#pragma once
#include "EnumMask.h"
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE

enum class ResourceType
{
    Buffer,
    Texture,
    Sampler,
};

enum class ResourceUsage : uint32_t
{
    ShaderRead,
    ShaderWrite,
    RenderTarget,
};

using ResourceUsageMask = EnumMask<ResourceUsage>;

struct ResourceViewSettings
{

};

struct ResourceViewId : ResourceViewSettings
{
    ResourceUsageMask usage;
    ResourceViewId(ResourceUsageMask usage, const ResourceViewSettings& settings = {})
        :ResourceViewSettings(settings), usage(usage)
    {
    }

    bool operator<(const ResourceViewId& other) const {
        if (usage.GetValue() < other.usage.GetValue()) return true;
        if (usage.GetValue() > other.usage.GetValue()) return false;
        return usage.GetValue() < other.usage.GetValue();
    }

    bool operator==(const ResourceViewId& other) const {
        return usage.GetValue() == other.usage.GetValue();
    }
};

enum class TextureDimensionType : uint32_t
{
    Tex1D = 0,
    Tex1DArray,
    Tex2D,
    Tex2DArray,
};

struct IResource : IObject
{
    virtual ResourceType GetResourceType() const = 0;
};
ARISENRHI_END_NAMESPACE
