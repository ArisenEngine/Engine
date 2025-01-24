#pragma once
#include "DescriptorPool.h"
#include "../RHICommon.h"

namespace ArisenEngine::RHI
{
    class DescriptorPool;
    class Device;
}

namespace ArisenEngine::RHI
{
    class RHIDescriptorSet
    {
    public:
        RHIDescriptorSet(DescriptorPool* descriptorPool, UInt32 layoutIndex):
        m_DescriptorPool(descriptorPool), m_LayoutIndex(layoutIndex)
        {
            
        }
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIDescriptorSet)
        VIRTUAL_DECONSTRUCTOR(RHIDescriptorSet)

        virtual void* GetHandle() = 0;

    public:
     
        DescriptorPool* GetDescriptorPool() { return m_DescriptorPool; }
        UInt32 GetLayoutIndex() const { return m_LayoutIndex; }

    protected:

    private:
        UInt32 m_LayoutIndex {0};
        DescriptorPool* m_DescriptorPool {nullptr};
    };
}
