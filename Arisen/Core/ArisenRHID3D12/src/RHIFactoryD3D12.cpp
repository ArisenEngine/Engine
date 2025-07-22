#include "RHIFactoryD3D12.h"

#include <wrl/client.h>
#include <d3d12sdklayers.h>

#include "RHIFactoryBase.h"
#include "../../../3rdparty/spdlog/include/spdlog/spdlog.h"
#include "Logger/DebugUtils.hpp"

using Microsoft::WRL::ComPtr;

ARISENRHI_BEGIN_NAMEPSACE
    class RHIFactoryD3D12Impl : public RHIFactoryBase<IRHIFactoryD3D12>
    {
    public:
        static RHIFactoryD3D12Impl* GetInstance()
        {
            static RHIFactoryD3D12Impl FactoryD3D12Impl;
            return &FactoryD3D12Impl;
        }

        RHIFactoryD3D12Impl()
            : RHIFactoryBase()
        {
        }

        virtual void CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo, IDevice** pOutDevice) override;
        virtual void CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain) override;
    };

    void RHIFactoryD3D12Impl::CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain)
    {
    }

    void RHIFactoryD3D12Impl::CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo, IDevice** pOutDevice)
    {
        //TODO: Check Engine version.
        LOG_RHI_DEBUG("Create DeviceD3D12."); 
        CHECK(pOutDevice, "pOutDevice is nullptr."); 

        try
        {
            // enable debug layer.
            if (InCreateInfo.EnableValidation)
            {
                ComPtr<ID3D12Debug> debugController;
                if (SUCCEEDED(D3D12GetDebugInterface(__uuidof(debugController),reinterpret_cast<void**>(static_cast<ID3D12Debug**>(&debugController)))))
                {
                    debugController->EnableDebugLayer();
                    
                }
            }
        }
        catch (const std::runtime_error& e)
        {
            LOG_ERROR("failed to create DeviceD3D12.");
            return;
        }
    }

    IRHIFactoryD3D12* CreateRHIFactoryD3D12()
    {
        return RHIFactoryD3D12Impl::GetInstance();
    }

ARISENRHI_END_NAMESPACE
