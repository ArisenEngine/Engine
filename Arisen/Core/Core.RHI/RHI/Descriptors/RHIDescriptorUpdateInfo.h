#pragma once
#include "../Samplers/RHISampler.h"
#include "../Core/RHICommon.h"
#include "../Handles/RHIHandle.h"
#include "RHI/Enums/Pipeline/EDescriptorType.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    class ImageView;

    typedef struct RHIDescriptorImageInfo
    {
        RHISamplerHandle        sampler;
        RHIImageViewHandle      imageView;
        EImageLayout   imageLayout;
        
    } RHIDescriptorImageInfo;
    
    typedef struct RHIDescriptorUpdateInfo
    {
        //layout binding
        UInt32 binding;
        EDescriptorType type;
        UInt32 descriptorCount;
        
        // DescriptorWrite 
        Containers::Vector<RHIDescriptorImageInfo>          imageInfo;
        Containers::Vector<RHIBufferHandle>                 bufferHandles;
        Containers::Vector<RHIImageViewHandle>              texelBufferViews; // Assuming texel buffers are treated as image views or similar handle
        
    } RHIDescriptorUpdateInfo;
}
