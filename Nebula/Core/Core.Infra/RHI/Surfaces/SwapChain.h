#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Image/ColorSpace.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Swapchain/PresentMode.h"
#include "RHI/Enums/Swapchain/SharingMode.h"
#include "RHI/Synchronization/RHISemaphore.h"

namespace NebulaEngine::RHI
{
    class Surface;
    class ImageHandle;
    class RHISemaphore;

    struct SwapChainDescriptor
    {
        u32 width { 0 }, height { 0 }, imageCount { 1 };
        /// specific image layer, should be always 1, unless in VR
        u32 imageArrayLayers { 1 };
        u32 imageUsageFlagBits { 0 };
        u32 queueFamilyIndexCount {2};
        
        Format colorFormat { FORMAT_R8G8B8_SRGB };
        ColorSpace colorSpace { COLOR_SPACE_SRGB_NONLINEAR };
        SharingMode sharingMode { SHARING_MODE_CONCURRENT };
        PresentMode presentMode { PRESENT_MODE_FIFO };
        
        bool clipped { true };
        u32 surfaceTransformFlagBits { 0 };
        u32 compositeAlphaFlagBits { 0 };
        u32 swapChainCreateFlags { 0 };
        std::optional<const void*> customData;
    };

    
    class SwapChain
    {
    public:
        NO_COPY_NO_MOVE(SwapChain)
        SwapChain() {}
        VIRTUAL_DECONSTRUCTOR(SwapChain)
        virtual void* GetHandle() const = 0;
        virtual void CreateSwapChainWithDesc(Surface* surface, SwapChainDescriptor desc) = 0;
        virtual RHISemaphore* GetImageAvailableSemaphore() const = 0;
        virtual RHISemaphore* GetRenderFinishSemaphore() const  = 0;
        virtual ImageHandle* AquireCurrentImage() = 0;
        virtual void Present() = 0;
    protected:
        SwapChainDescriptor m_Desc;
        virtual void RecreateSwapChainIfNeeded() = 0;

    public:
        void SetResolution(u32 width, u32 height)
        {
            if (m_Desc.width == width && m_Desc.height == height)
            {
                return;
            }

            m_Desc.width = width;
            m_Desc.height = height;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageCount(u32 count)
        {
            if (count == m_Desc.imageCount)
            {
                return;
            }

            m_Desc.imageCount = count;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageArrayLayers(u32 layers)
        {
            if (m_Desc.imageArrayLayers == layers)
            {
                return;
            }

            m_Desc.imageArrayLayers = layers;
            
            RecreateSwapChainIfNeeded();
        }
        
        void SetImageFormat(Format format)
        {
            if (format == m_Desc.colorFormat)
            {
                return;
            }

            m_Desc.colorFormat = format;
            RecreateSwapChainIfNeeded();
        }
        
        void SetColorSpace(ColorSpace colorSpace)
        {
            if (m_Desc.colorSpace == colorSpace)
            {
                return;
            }

           m_Desc. colorSpace = colorSpace;
            RecreateSwapChainIfNeeded();
        }
        
        void SetImageUsage(u32 usage)
        {
            if (usage == m_Desc.imageUsageFlagBits)
            {
                return;
            }
            
            m_Desc.imageUsageFlagBits = usage;
            RecreateSwapChainIfNeeded();
        }

        u32 GetCurrentImageUsage() const
        {
            return m_Desc.imageUsageFlagBits;
        }

        void SetSharingMode(SharingMode mode)
        {
            if (mode == m_Desc.sharingMode)
            {
                return;
            }
            m_Desc.sharingMode = mode;
            RecreateSwapChainIfNeeded();
        }
    };
}
