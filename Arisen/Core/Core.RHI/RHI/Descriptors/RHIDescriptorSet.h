#pragma once
#include "RHIDescriptorPool.h"
#include "../Core/RHICommon.h"

namespace ArisenEngine::RHI
{
    class RHIDescriptorPool;
    class RHIDevice;
}

namespace ArisenEngine::RHI
{
    class RHIDescriptorSet
    {
    public:
        RHIDescriptorSet(RHIDescriptorPool* descriptorPool, UInt32 layoutIndex):
        m_DescriptorPool(descriptorPool), m_LayoutIndex(layoutIndex)
        {
            
        }
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIDescriptorSet)
        VIRTUAL_DECONSTRUCTOR(RHIDescriptorSet)

        virtual void* GetHandle() = 0;

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
