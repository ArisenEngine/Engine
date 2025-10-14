#pragma once
#include "RHISampler.h"
#include "../RHICommon.h"
#include "../Memory/BufferView.h"
#include "../Handles/BufferHandle.h"
#include "../Memory/ImageView.h"
#include "RHI/Enums/Pipeline/EDescriptorType.h"
#include "RHI/Enums/Image/EImageLayout.h"

namespace ArisenEngine::RHI
{
    class ImageView;

    typedef struct RHIDescriptorImageInfo
    {
        RHISampler*        sampler;
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
