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
        virtual void* GetHandle() const = 0;
        RHIDevice* GetDevice() const
        {
            return m_Device;
        }
    private:
        RHIDevice* m_Device;
    };
}
