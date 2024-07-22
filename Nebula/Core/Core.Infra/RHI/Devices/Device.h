#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Instance.h"
namespace NebulaEngine::RHI
{
    class Device
    {
        
        
    public:
        NO_COPY_NO_MOVE(Device)
        virtual ~Device() noexcept
        {
            m_Instance = nullptr;
        }

        virtual void* GetHandle() const = 0; 
        
    protected:
        Instance* m_Instance;
        Device(Instance* instance): m_Instance(instance) {}
        
    private:

    };
}
