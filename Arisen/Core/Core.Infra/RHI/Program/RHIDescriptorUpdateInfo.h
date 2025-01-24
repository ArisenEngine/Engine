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
    
    typedef struct RHIDescriptorUpdateInfo
    {
        //layout binding
        UInt32 binding;
        EDescriptorType type;
        UInt32 descriptorCount;
        
        // DescriptorWrite 
        Containers::Vector<RHIDescriptorImageInfo>          imageInfo;
        Containers::Vector<std::shared_ptr<BufferHandle>>   bufferHaneles;
        Containers::Vector<BufferView*>                     texelBufferViews;
        
    } RHIDescriptorUpdateInfo;
}
