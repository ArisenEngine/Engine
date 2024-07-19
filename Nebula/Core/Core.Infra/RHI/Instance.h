


#pragma once
#include "../Common/CommandHeaders.h"
#include "../Common/PrimitiveTypes.h"


namespace NebulaEngine::RHI
{
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
        u32 variant, major, minor, patch;
        /** App Version */
        u32 appMajor, appMinor, appPatch;
        /** App Version */
        u32 engineMajor, engineMinor, enginePatch;
    };
    
    class Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(Instance)
        VIRTUAL_DECONSTRUCTOR(Instance)
        
        Instance(InstanceInfo&& instance_info)
        {
            
        }
        
        bool IsEnableValidation()
        {
            return m_EnableValidation;
        }

        virtual void* GetHandle() const = 0;
        virtual void* GetLogicalDevice(u32&& windowId) const = 0;
        virtual void InitLogicDevices() = 0;
        virtual void PickPhysicalDevice(bool considerSurface = false) = 0;

        virtual bool IsSupportLinearColorSpace(u32&& windowId) = 0;
        virtual bool PresentModeSupported(u32&& windowId, PresentMode mode) = 0;
        virtual void SetCurrentPresentMode(u32&& windowId, PresentMode mode) = 0;
        virtual const Format GetSuitableSwapChainFormat(u32&& windowId) = 0;
        virtual const PresentMode GetSuitablePresentMode(u32&& windowId) = 0;
        
        /// \brief used for DXC args
        /// \return api env value
        virtual const std::wstring GetEnvString() const = 0;

        virtual void CreateSurface(u32&& windowId) = 0;
        virtual void DestroySurface(u32&& windowId) = 0;
        virtual const Surface& GetSurface(u32&& windowId) = 0;
        virtual void SetResolution(const u32&& windowId, const u32&& width, const u32&& height) = 0;


        
        
    protected:
        
        bool m_EnableValidation { false };
        virtual void CheckSwapChainCapabilities() = 0;
    };
}

