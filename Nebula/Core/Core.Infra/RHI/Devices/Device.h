#pragma once
#include "../../Common/CommandHeaders.h"
#include "../Instance.h"
#include "../RHICommon.h"
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
        virtual void DeviceWaitIdle() const = 0;
        virtual u32 CreateGPUProgram() = 0;
        virtual void DestroyGPUProgram(u32 programId) = 0;
        virtual bool AttachProgramByteCode(u32 programId, GPUProgramDesc&& desc) = 0;
    protected:
        Instance* m_Instance;
        Device(Instance* instance): m_Instance(instance) {}
    private:

    };
}
