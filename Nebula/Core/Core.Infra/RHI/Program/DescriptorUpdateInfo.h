#pragma once
#include "Sampler.h"
#include "../RHICommon.h"
#include "../Memory/BufferView.h"

namespace NebulaEngine::RHI
{
    typedef struct DescriptorImageInfo
    {
        Sampler*        sampler;
        ImageView*      imageView;
        EImageLayout   imageLayout;
        
    } DescriptorImageInfo;

    typedef struct DescriptorBufferInfo
    {
        BufferHandle*    bufferHandle;
        u64             offset;
        u64             range;
        
    } DescriptorBufferInfo;
    
    typedef struct DescriptorUpdateInfo
    {
        //layout binding
        u32 binding;
        EDescriptorType type;
        u32 descriptorCount;
        
        // DescriptorWrite 
        const DescriptorImageInfo*      pImageInfo;
        const DescriptorBufferInfo*     pBufferInfo;
        const BufferView*               pTexelBufferView;
        
    } DescriptorUpdateInfo;
}
