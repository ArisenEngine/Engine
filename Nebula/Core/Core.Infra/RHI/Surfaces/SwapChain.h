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
        u32 m_Width { 0 }, m_Height { 0 }, m_ImageCount { 1 };
        /// specific image layer, should be always 1, unless in VR
        u32 m_ImageArrayLayers { 1 };
        u32 m_ImageUsageFlagBits { 0 };
        u32 m_QueueFamilyIndexCount {2};
        
        Format m_ColorFormat { FORMAT_R8G8B8_SRGB };
        ColorSpace m_ColorSpace { COLOR_SPACE_SRGB_NONLINEAR };
        SharingMode m_SharingMode { SHARING_MODE_CONCURRENT };
        PresentMode m_PresentMode { PRESENT_MODE_FIFO };
        
        bool clipped { true };
        u32 m_SurfaceTransformFlagBits { 0 };
        u32 m_CompositeAlphaFlagBits { 0 };
        u32 m_SwapChainCreateFlags { 0 };
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
        virtual const RHISemaphore* GetImageAvailableSemaphore() const = 0;
        virtual const RHISemaphore* GetRenderFinishSemaphore() const  = 0;
        virtual ImageHandle* AquireCurrentImage() = 0;
        virtual void Present() = 0;
    protected:
        SwapChainDescriptor m_Desc;
        virtual void RecreateSwapChainIfNeeded() = 0;

    public:
        void SetResolution(u32 width, u32 height)
        {
            if (m_Desc.m_Width == width && m_Desc.m_Height == height)
            {
                return;
            }

            m_Desc.m_Width = width;
            m_Desc.m_Height = height;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageCount(u32 count)
        {
            if (count == m_Desc.m_ImageCount)
            {
                return;
            }

            m_Desc.m_ImageCount = count;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageArrayLayers(u32 layers)
        {
            if (m_Desc.m_ImageArrayLayers == layers)
            {
                return;
            }

            m_Desc.m_ImageArrayLayers = layers;
            
            RecreateSwapChainIfNeeded();
        }
        
        void SetImageFormat(Format format)
        {
            if (format == m_Desc.m_ColorFormat)
            {
                return;
            }

            m_Desc.m_ColorFormat = format;
            RecreateSwapChainIfNeeded();
        }
        
        void SetColorSpace(ColorSpace colorSpace)
        {
            if (m_Desc.m_ColorSpace == colorSpace)
            {
                return;
            }

           m_Desc. m_ColorSpace = colorSpace;
            RecreateSwapChainIfNeeded();
        }
        
        void SetImageUsage(u32 usage)
        {
            if (usage == m_Desc.m_ImageUsageFlagBits)
            {
                return;
            }
            
            m_Desc.m_ImageUsageFlagBits = usage;
            RecreateSwapChainIfNeeded();
        }

        u32 GetCurrentImageUsage() const
        {
            return m_Desc.m_ImageUsageFlagBits;
        }

        void SetSharingMode(SharingMode mode)
        {
            if (mode == m_Desc.m_SharingMode)
            {
                return;
            }
            m_Desc.m_SharingMode = mode;
            RecreateSwapChainIfNeeded();
        }
    };
}
