#pragma once
#include "../../Common/CommandHeaders.h"
#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{

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

        std::optional<VkQueueFamilyIndices> m_VkQueueFamilyIndices;

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
            ASSERT(count > 0);
            if (count == m_Desc.m_ImageCount)
            {
                return;
            }

            m_Desc.m_ImageCount = count;

            RecreateSwapChainIfNeeded();
        }
        
        void SetImageArrayLayers(u32 layers)
        {
            ASSERT(layers >= 1);
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

        virtual void CreateSwapChainWithDesc(SwapChainDescriptor desc) = 0;
    protected:
        SwapChainDescriptor m_Desc;
        virtual void RecreateSwapChainIfNeeded() = 0;
    };
}
