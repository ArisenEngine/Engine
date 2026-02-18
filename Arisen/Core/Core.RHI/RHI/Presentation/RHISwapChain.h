#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Image/EColorSpace.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Swapchain/EPresentMode.h"
#include "RHI/Enums/Memory/ESharingMode.h"
#include "RHI/Sync/RHISemaphore.h"
#include "../Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    class RHISurface;
    class RHISemaphore;

    struct RHISwapChainDescriptor
    {
        UInt32 width { 0 }, height { 0 }, imageCount { 1 };
        /// specific image layer, should be always 1, unless in VR
        UInt32 imageArrayLayers { 1 };
        UInt32 imageUsageFlagBits { 0 };
        UInt32 queueFamilyIndexCount {2};
        
        EFormat colorFormat { FORMAT_R8G8B8_SRGB };
        EColorSpace colorSpace { COLOR_SPACE_SRGB_NONLINEAR };
        ESharingMode sharingMode { SHARING_MODE_CONCURRENT };
        EPresentMode presentMode { PRESENT_MODE_FIFO };
        
        bool clipped { true };
        UInt32 surfaceTransformFlagBits { 0 };
        UInt32 compositeAlphaFlagBits { 0 };
        UInt32 swapChainCreateFlags { 0 };
        // TODO(CppSharp-P0): std::optional<const void*> 双重问题：
        // 1) std::optional 非 POD   2) const void* 无类型。
        // 替换为 const void* customData = nullptr; 用 nullptr 表示无自定义数据。
        std::optional<const void*> customData;
    };

    
    class RHISwapChain
    {
    public:
        NO_COPY_NO_MOVE(RHISwapChain)
        RHISwapChain(UInt32 maxFramesInFlight): m_MaxFramesInFlight(maxFramesInFlight) {}
        VIRTUAL_DECONSTRUCTOR(RHISwapChain)
        // TODO(CppSharp-P0): GetHandle() 返回 void*，泄漏 VkSwapchainKHR。移至后端或删除。
        virtual void* GetHandle() const = 0;
        virtual void CreateSwapChainWithDesc(RHISwapChainDescriptor desc) = 0;

        virtual RHIImageHandle BeginFrame(UInt32 frameIndex) = 0;
        virtual void EndFrame(UInt32 frameIndex) = 0;

        // TODO(CppSharp-P1): 以下 4 个 deprecated 方法应在 CppSharp 导出前删除。
        // 当前测试代码仍使用 GetImageView() 和 Present()，需先迁移到 BeginFrame/EndFrame。
        [[deprecated("Use BeginFrame instead")]]
        virtual RHISemaphoreHandle GetImageAvailableSemaphore(UInt32 frameIndex) const = 0;
        [[deprecated("Use EndFrame instead")]]
        virtual RHISemaphoreHandle GetRenderFinishSemaphore(UInt32 frameIndex) const  = 0;
        [[deprecated("Use BeginFrame instead")]]
        virtual RHIImageHandle AcquireCurrentImage(UInt32 frameIndex) = 0;
        [[deprecated("Use BeginFrame or manual tracking instead")]]
        virtual RHIImageViewHandle GetImageView(UInt32 frameIndex) const = 0;
        [[deprecated("Use EndFrame instead")]]
        virtual void Present(UInt32 frameIndex) = 0;

        virtual void Cleanup() = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
        RHISwapChainDescriptor m_Desc;
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
        
        void SetColorSpace(EColorSpace colorSpace)
        {
            if (m_Desc.colorSpace == colorSpace)
            {
                return;
            }

           m_Desc.colorSpace = colorSpace;
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

