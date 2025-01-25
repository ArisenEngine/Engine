#pragma once
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Image/ColorSpace.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Swapchain/PresentMode.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Synchronization/RHISemaphore.h"

namespace ArisenEngine::RHI
{
    class Surface;
    class ImageHandle;
    class RHISemaphore;

    struct SwapChainDescriptor
    {
        UInt32 width { 0 }, height { 0 }, imageCount { 1 };
        /// specific image layer, should be always 1, unless in VR
        UInt32 imageArrayLayers { 1 };
        UInt32 imageUsageFlagBits { 0 };
        UInt32 queueFamilyIndexCount {2};
        
        EFormat colorFormat { FORMAT_R8G8B8_SRGB };
        ColorSpace colorSpace { COLOR_SPACE_SRGB_NONLINEAR };
        ESharingMode sharingMode { SHARING_MODE_CONCURRENT };
        PresentMode presentMode { PRESENT_MODE_FIFO };
        
        bool clipped { true };
        UInt32 surfaceTransformFlagBits { 0 };
        UInt32 compositeAlphaFlagBits { 0 };
        UInt32 swapChainCreateFlags { 0 };
        std::optional<const void*> customData;
    };

    
    class SwapChain
    {
    public:
        NO_COPY_NO_MOVE(SwapChain)
        SwapChain(UInt32 maxFramesInFlight): m_MaxFramesInFlight(maxFramesInFlight) {}
        VIRTUAL_DECONSTRUCTOR(SwapChain)
        virtual void* GetHandle() const = 0;
        virtual void CreateSwapChainWithDesc(SwapChainDescriptor desc) = 0;
        virtual RHISemaphore* GetImageAvailableSemaphore(UInt32 frameIndex) const = 0;
        virtual RHISemaphore* GetRenderFinishSemaphore(UInt32 frameIndex) const  = 0;
        virtual ImageHandle* AquireCurrentImage(UInt32 frameIndex) = 0;
        virtual void Present(UInt32 frameIndex) = 0;
        virtual void Cleanup() = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
        SwapChainDescriptor m_Desc;
        virtual void RecreateSwapChainIfNeeded() = 0;

    public:
        void SetResolution(UInt32 width, UInt32 height)
        {
            if (m_Desc.width == width && m_Desc.height == height)
            {
                return;
            }

            m_Desc.width = width;
            m_Desc.height = height;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageCount(UInt32 count)
        {
            if (count == m_Desc.imageCount)
            {
                return;
            }

            m_Desc.imageCount = count;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageArrayLayers(UInt32 layers)
        {
            if (m_Desc.imageArrayLayers == layers)
            {
                return;
            }

            m_Desc.imageArrayLayers = layers;
            
            RecreateSwapChainIfNeeded();
        }
        
        void SetImageFormat(EFormat format)
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
        
        void SetImageUsage(UInt32 usage)
        {
            if (usage == m_Desc.imageUsageFlagBits)
            {
                return;
            }
            
            m_Desc.imageUsageFlagBits = usage;
            RecreateSwapChainIfNeeded();
        }

        UInt32 GetCurrentImageUsage() const
        {
            return m_Desc.imageUsageFlagBits;
        }

        void SetSharingMode(ESharingMode mode)
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
