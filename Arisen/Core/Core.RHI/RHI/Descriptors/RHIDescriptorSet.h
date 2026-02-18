#pragma once
#include "RHIDescriptorPool.h"
#include "../Core/RHICommon.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHIDescriptorPool;
    class RHIDevice;
}

namespace ArisenEngine::RHI
{
    class RHI_DLL RHIDescriptorSet
    {
    public:
        RHIDescriptorSet(RHIDescriptorPool* descriptorPool, UInt32 layoutIndex);
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIDescriptorSet)
        virtual ~RHIDescriptorSet() noexcept;

        // TODO(CppSharp-P0): GetHandle() \u8fd4\u56de void*\uff0c\u6cc4\u6f0f VkDescriptorSet\u3002\u5e94\u79fb\u81f3\u540e\u7aef\u3002\r\n        virtual void* GetHandle() = 0;

        virtual bool IsBindless() const { return false; }
        virtual const Containers::Vector<UInt32>& GetBindlessIndices() const 
        { 
            static Containers::Vector<UInt32> empty; 
            return empty; 
        }

    public:
     
        RHIDescriptorPool* GetDescriptorPool() { return m_DescriptorPool; }
        UInt32 GetLayoutIndex() const { return m_LayoutIndex; }

    protected:

    private:
        UInt32 m_LayoutIndex {0};
        RHIDescriptorPool* m_DescriptorPool {nullptr};
    };
}
