#pragma once
#include "DeviceLimits.h"
#include "../Common/CommandHeaders.h"
#include "../Common/PrimitiveTypes.h"
#include "Devices/Device.h"
#include "Enums/Image/EFormat.h"
#include "Enums/Swapchain/PresentMode.h"


namespace ArisenEngine::RHI
{
    class Device;
    class Surface;

    struct InstanceInfo
    {
        /** app name */
        const char* name;
        /** engine name */
        const char* engineName;
        /** enable validation layer */
        bool validationLayer;
        /** API Version */
        UInt32 variant, major, minor, patch;
        /** App Version */
        UInt32 appMajor, appMinor, appPatch;
        /** App Version */
        UInt32 engineMajor, engineMinor, enginePatch;
        UInt32 maxFramesInFlight;
    };
    
    COREINFRA_DLL class Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(Instance)
        VIRTUAL_DECONSTRUCTOR(Instance)
        
        Instance(InstanceInfo&& instance_info): m_MaxFramesInFlight(instance_info.maxFramesInFlight)
        {
            
        }
        
        bool IsEnableValidation()
        {
            return m_EnableValidation;
        }

        virtual void* GetHandle() const = 0;
        virtual void InitLogicDevices() = 0;
        virtual void PickPhysicalDevice(bool considerSurface = false) = 0;

        virtual bool IsSupportLinearColorSpace(UInt32&& windowId) = 0;
        virtual bool PresentModeSupported(UInt32&& windowId, PresentMode mode) = 0;
        virtual void SetCurrentPresentMode(UInt32&& windowId, PresentMode mode) = 0;
        virtual const EFormat GetSuitableSwapChainFormat(UInt32&& windowId) = 0;
        virtual const PresentMode GetSuitablePresentMode(UInt32&& windowId) = 0;
        
        /// \brief used for DXC args
        /// \return api env value
        virtual const std::wstring GetEnvString() const = 0;

        virtual void CreateSurface(UInt32&& windowId) = 0;
        virtual void DestroySurface(UInt32&& windowId) = 0;
        virtual Surface& GetSurface(UInt32&& windowId) = 0;
        virtual void SetResolution(const UInt32&& windowId, const UInt32&& width, const UInt32&& height) = 0;

        virtual void UpdateSurfaceCapabilities(Surface* surface) = 0;

        virtual bool IsPhysicalDeviceAvailable() const = 0;
        virtual bool IsSurfacesAvailable() const = 0;
        
        virtual void CreateLogicDevice(UInt32 windowId) = 0;
        virtual Device* GetLogicalDevice(UInt32 windowId) = 0;

        virtual const UInt32 GetExternalIndex() const = 0;

        const UInt32 GetMaxFramesInFlight() const
        {
            return m_MaxFramesInFlight;
        }

        virtual RHIDeviceLimits GetDeviceLimits() const = 0;
        
    protected:
        UInt32 m_MaxFramesInFlight;
        bool m_EnableValidation { false };
        virtual void CheckSwapChainCapabilities() = 0;
    };
}

