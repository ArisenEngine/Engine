#pragma once
#include "../Definitions/RHICapabilities.h"
#include "Base/FoundationMinimal.h"
#include "Base/PrimitiveTypes.h"
#include "RHIDevice.h"
#include "../Enums/Image/EFormat.h"
#include "../Enums/Swapchain/EPresentMode.h"


namespace ArisenEngine::RHI
{
    class RHIDevice;
    class RHISurface;
}

#include "../Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    struct RHIInstanceInfo
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

    class RHI_DLL RHIInstance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIInstance)
        VIRTUAL_DECONSTRUCTOR(RHIInstance)

        explicit RHIInstance(RHIInstanceInfo&& instance_info): m_Capabilities(),
                                                               m_MaxFramesInFlight(instance_info.maxFramesInFlight)
        {
        }

        inline bool IsEnableValidation() const
        {
            return m_EnableValidation;
        }

        virtual void* GetHandle() const = 0;
        virtual void InitLogicDevices() = 0;
        virtual void PickPhysicalDevice(bool considerSurface = false) = 0;

        virtual bool IsSupportLinearColorSpace(UInt32 windowId) = 0;
        virtual bool PresentModeSupported(UInt32 windowId, EPresentMode mode) = 0;
        virtual void SetCurrentPresentMode(UInt32 windowId, EPresentMode mode) = 0;
        virtual EFormat GetSuitableSwapChainFormat(UInt32 windowId) = 0;
        virtual EPresentMode GetSuitablePresentMode(UInt32 windowId) = 0;

        /// \brief used for DXC args
        /// \return api env value
        virtual String GetEnvString() const = 0;

        virtual void CreateSurface(UInt32 windowId) = 0;
        virtual void DestroySurface(UInt32 windowId) = 0;
        virtual RHISurface& GetSurface(UInt32 windowId) = 0;
        virtual void SetResolution(UInt32 windowId, UInt32 width, UInt32 height) = 0;

        virtual void UpdateSurfaceCapabilities(RHISurface* surface) = 0;

        virtual bool IsPhysicalDeviceAvailable() const = 0;
        virtual bool IsSurfacesAvailable() const = 0;

        virtual void CreateLogicDevice(UInt32 windowId) = 0;
        virtual RHIDevice* GetLogicalDevice(UInt32 windowId) = 0;

        virtual UInt32 GetExternalIndex() const = 0;

        inline UInt32 GetMaxFramesInFlight() const
        {
            return m_MaxFramesInFlight;
        }

        inline RHICapabilities GetCapabilities() const
        {
            return m_Capabilities;
        }

    protected:
        RHICapabilities m_Capabilities;
        UInt32 m_MaxFramesInFlight;
        bool m_EnableValidation{false};
        virtual void CheckSwapChainCapabilities() = 0;
    };
}
