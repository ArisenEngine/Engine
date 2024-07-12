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
        Instance* m_Instance;
        Device(Instance* instance): m_Instance(instance) {}
        
    private:

        virtual void CreateSurface(u32&& windowId) = 0;
        virtual void DestroySurface(u32&& windowId) = 0;
        virtual const Surface& GetSurface(u32&& windowId) = 0;
        virtual void SetResolution(const u32&& windowId, const u32&& width, const u32&& height) = 0;

        virtual void CreateLogicDevice(u32 windowId) = 0;
        virtual void* GetLogicalDevice(u32 windowId) = 0;
        virtual void InitLogicDevices() = 0;
        virtual void CreateSwapChain(u32 windowId) = 0;
        virtual bool IsPhysicalDeviceAvailable() const = 0;
        virtual bool IsSurfaceAvailable() const = 0;

        virtual void CheckSwapChainCapabilities() = 0;
        virtual void InitDefaultSwapChains() = 0;
    };
}
