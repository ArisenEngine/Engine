#pragma once
#include "DeviceLimits.h"
#include "../Common/CommandHeaders.h"
#include "../Common/PrimitiveTypes.h"
#include "Devices/RHIDevice.h"
#include "Enums/Image/EFormat.h"
#include "Enums/Swapchain/PresentMode.h"


namespace ArisenEngine::RHI
{
    class RHIDevice;
    class Surface;
    class RHIFactory;
}

namespace ArisenEngine::RHI
{
    
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
    
    COREINFRA_DLL class RHIInstance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIInstance)
        VIRTUAL_DECONSTRUCTOR(RHIInstance)

        explicit RHIInstance(InstanceInfo&& instance_info): m_DeviceLimits(),
                                                         m_MaxFramesInFlight(instance_info.maxFramesInFlight)
        {
        }

        bool IsEnableValidation() const
        {
            return m_EnableValidation;
        }

        virtual void* GetHandle() const = 0;
        virtual void InitLogicDevices() = 0;
        virtual void PickPhysicalDevice(bool considerSurface = false) = 0;

        virtual bool IsSupportLinearColorSpace(UInt32 windowId) = 0;
        virtual bool PresentModeSupported(UInt32 windowId, PresentMode mode) = 0;
        virtual void SetCurrentPresentMode(UInt32 windowId, PresentMode mode) = 0;
        virtual EFormat GetSuitableSwapChainFormat(UInt32 windowId) = 0;
        virtual PresentMode GetSuitablePresentMode(UInt32 windowId) = 0;
        
        /// \brief used for DXC args
        /// \return api env value
        virtual std::wstring GetEnvString() const = 0;

        virtual void CreateSurface(UInt32 windowId) = 0;
        virtual void DestroySurface(UInt32 windowId) = 0;
        virtual Surface& GetSurface(UInt32 windowId) = 0;
        virtual void SetResolution(UInt32 windowId, UInt32 width, UInt32 height) = 0;

        virtual void UpdateSurfaceCapabilities(Surface* surface) = 0;

        virtual bool IsPhysicalDeviceAvailable() const = 0;
        virtual bool IsSurfacesAvailable() const = 0;
        
        virtual void CreateLogicDevice(UInt32 windowId) = 0;
        virtual RHIDevice* GetLogicalDevice(UInt32 windowId) = 0;

        virtual UInt32 GetExternalIndex() const = 0;

        UInt32 GetMaxFramesInFlight() const
        {
            return m_MaxFramesInFlight;
        }

        RHIDeviceLimits GetDeviceLimits() const
        {
            return m_DeviceLimits;
        };

        virtual RHIFactory* CreateFactory() = 0;
        
    protected:
        
        RHIDeviceLimits m_DeviceLimits;
        UInt32 m_MaxFramesInFlight;
        bool m_EnableValidation { false };
        virtual void CheckSwapChainCapabilities() = 0;
    };
}

