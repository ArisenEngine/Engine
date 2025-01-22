#pragma once
#include "Sampler.h"
#include "../RHICommon.h"
#include "../Memory/BufferView.h"

namespace ArisenEngine::RHI
{
    typedef struct RHIDescriptorImageInfo
    {
        Sampler*        sampler;
        ImageView*      imageView;
        EImageLayout   imageLayout;
        
    } RHIDescriptorImageInfo;

    typedef struct RHIDescriptorBufferInfo
    {
        BufferHandle*    bufferHandle;
        UInt64             offset;
        UInt64             range;
        
    } RHIDescriptorBufferInfo;
    
    typedef struct RHIDescriptorUpdateInfo
    {
        //layout binding
        UInt32 binding;
        EDescriptorType type;
        UInt32 descriptorCount;
        
        // DescriptorWrite 
        const RHIDescriptorImageInfo*      pImageInfo;
        const RHIDescriptorBufferInfo*     pBufferInfo;
        const BufferView*               pTexelBufferView;
        
    } RHIDescriptorUpdateInfo;
}
