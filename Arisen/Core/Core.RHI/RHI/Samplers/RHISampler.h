#pragma once
#include "../Core/RHICommon.h"
#include "../Core/RHIDevice.h"
#include "RHI/Enums/Sampler/EBorderColor.h"
#include "RHI/Enums/Sampler/ECompareOp.h"
#include "RHI/Enums/Sampler/EFilter.h"
#include "RHI/Enums/Sampler/ESamplerAddressMode.h"
#include "RHI/Enums/Sampler/ESamplerMipmapMode.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    typedef struct RHISamplerDesc
    {
        EFilter magFilter;
        EFilter minFilter;
        ESamplerMipmapMode mipmapMode;
        ESamplerAddressMode addressModeU;
        ESamplerAddressMode addressModeV;
        ESamplerAddressMode addressModeW;
        Float32 mipLodBias;
        bool anisotropyEnable;
        Float32 maxAnisotropy;
        bool compareEnable;
        ECompareOp compareOp;
        Float32 minLod;
        Float32 maxLod;
        EBorderColor borderColor;
        bool unnormalizedCoordinates;
    } RHISamplerDesc;
    
    class RHI_DLL RHISampler
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHISampler)
        virtual ~RHISampler() noexcept;
        RHISampler(RHIDevice* device);
        // TODO(CppSharp-P0): GetHandle() \u8fd4\u56de void*\uff0c\u6cc4\u6f0f VkSampler\u3002\u5e94\u79fb\u81f3\u540e\u7aef\u3002\r\n        // \u4e0a\u5c42\u901a\u8fc7 RHISamplerHandle \u5f15\u7528 Sampler\uff0c\u4e0d\u9700\u8981\u539f\u751f\u53e5\u67c4\u3002\r\n        virtual void* GetHandle() const = 0;
        RHIDevice* GetDevice() const
        {
            return m_Device;
        }
    private:
        RHIDevice* m_Device;
    };
}
