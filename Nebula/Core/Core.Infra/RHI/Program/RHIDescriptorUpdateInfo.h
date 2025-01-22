#pragma once
#include "Sampler.h"
#include "../RHICommon.h"
#include "../Memory/BufferView.h"

namespace NebulaEngine::RHI
{
    typedef struct RHIDescriptorImageInfo
    {
        Sampler*        sampler;
        ImageView*      imageView;
        EImageLayout   imageLayout;
        
    } DescriptorImageInfo;

    typedef struct RHIDescriptorBufferInfo
    {
        BufferHandle*    bufferHandle;
        UInt64             offset;
        UInt64             range;
        
    } DescriptorBufferInfo;
    
    typedef struct RHIDescriptorUpdateInfo
    {
        //layout binding
        UInt32 binding;
        EDescriptorType type;
        UInt32 descriptorCount;
        
        // DescriptorWrite 
        const DescriptorImageInfo*      pImageInfo;
        const DescriptorBufferInfo*     pBufferInfo;
        const BufferView*               pTexelBufferView;
        
    } DescriptorUpdateInfo;
}
