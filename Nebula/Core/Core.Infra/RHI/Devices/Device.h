#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Instance.h"
namespace NebulaEngine::RHI
{
    class Device
    {
        
        
    public:
        NO_COPY_NO_MOVE(Device)
        VIRTUAL_DECONSTRUCTOR(Device)
        
    protected:
        Instance& m_Instance;
        Device(Instance& instance): m_Instance(instance) {}
    private:

        
    };
}
