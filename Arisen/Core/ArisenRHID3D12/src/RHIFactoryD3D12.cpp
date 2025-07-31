#include "RHIFactoryD3D12.h"

#include <wrl/client.h>
#include <d3d12sdklayers.h>
#include "Logger/DebugUtils.h"
#include "CommonFlags.hpp"
#include "DeviceD3D12.h"
#include "dxgi1_4.h"

using Microsoft::WRL::ComPtr;

ARISENRHI_BEGIN_NAMEPSACE
RHIFactoryD3D12* RHIFactoryD3D12::GetInstance()
{
    static RHIFactoryD3D12 FactoryD3D12Impl;
    return &FactoryD3D12Impl;
}

Ptr<IDevice> RHIFactoryD3D12::CreateDeviceD3D12(const EngineCreateInfoD3D12& InCreateInfo)
{
    //TODO: Check Engine version.
    LOG_RHI_DEBUG("Create DeviceD3D12.");

    ComPtr<ID3D12Device> d3d12Device;

    try
    {
        // enable debug layer.
        if (InCreateInfo.EnableValidation)
        {
            ComPtr<ID3D12Debug> debugController;
            if (SUCCEEDED(
                D3D12GetDebugInterface(__uuidof(debugController),reinterpret_cast<void**>(static_cast<ID3D12Debug**>
                    (&debugController)))))
            {
                debugController->EnableDebugLayer();
                if (HasFlag(InCreateInfo.ValidationFlags,
                            D3D12_VALIDATION_FLAGS::D3D12_VALIDATION_FLAG_GPU_BASED_VALIDATION))
                {
                    LOG_RHI_DEBUG("Enable gpu based invalidation!");
                    ComPtr<ID3D12Debug1> debugController1;
                    debugController->QueryInterface(IID_PPV_ARGS(&debugController1));
                    if (debugController1)
                    {
                        debugController1->SetEnableGPUBasedValidation(true);
                    }
                }
            }
        }

        ComPtr<IDXGIFactory4> factory;
        HRESULT hr = CreateDXGIFactory1(__uuidof(factory),
                                        reinterpret_cast<void**>(static_cast<IDXGIFactory4**>(&factory)));
        CHECK_D3D_HR(hr, "failed to create dxgi factory.");

        D3D_FEATURE_LEVEL MinFeatureLevel(D3D_FEATURE_LEVEL::D3D_FEATURE_LEVEL_11_0);
        uint32_t AdapterId = InCreateInfo.AdapterId;

        // find adapter.
        ComPtr<IDXGIAdapter1> adapter;
        if (AdapterId == DEFAULT_ADAPTER_ID)
        {
            for (UINT AdapterIndex = 0; factory->EnumAdapters1(AdapterIndex, &adapter) != DXGI_ERROR_NOT_FOUND; ++
                 AdapterIndex, adapter->Release())
            {
                DXGI_ADAPTER_DESC1 desc;
                adapter->GetDesc1(&desc);

                if (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE)
                {
                    continue;
                }

                if (SUCCEEDED(D3D12CreateDevice(adapter.Get(), MinFeatureLevel, _uuidof(ID3D12Device), nullptr)))
                {
                    LOG_RHI_DEBUG("adapter found.");
                    break;
                }
            }

            CHECK(adapter, "no suitable hardware adapter found for d3d12.");
        }
        else
        {
            CHECK(nullptr, "specified adapter id is not implemented.");
        }

        const D3D_FEATURE_LEVEL FeatureLevels[] = {
            D3D_FEATURE_LEVEL_12_1, D3D_FEATURE_LEVEL_12_0, D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0
        };
        for (auto FeatureLevel : FeatureLevels)
        {
            hr = D3D12CreateDevice(adapter.Get(), FeatureLevel, IID_PPV_ARGS(&d3d12Device));
            if (SUCCEEDED(hr))
            {
                LOG_RHI_DEBUG("device created.");
                break;
            }
        }

        // create from soft ware.
        if (FAILED(hr))
        {
            CHECK(0, "failed to create hardware deice, and software device is not implemented!");
        }

        // config debug output.
        if (InCreateInfo.EnableValidation)
        {
            ComPtr<ID3D12InfoQueue> pInfoQueue;
            hr = d3d12Device->QueryInterface(IID_PPV_ARGS(&pInfoQueue));
            if (SUCCEEDED(hr))
            {
                D3D12_MESSAGE_SEVERITY Severities[] = {D3D12_MESSAGE_SEVERITY_INFO};

                D3D12_MESSAGE_ID DenyIds[] = {
                    D3D12_MESSAGE_ID_CLEARRENDERTARGETVIEW_MISMATCHINGCLEARVALUE,
                    D3D12_MESSAGE_ID_CLEARDEPTHSTENCILVIEW_MISMATCHINGCLEARVALUE
                };

                D3D12_INFO_QUEUE_FILTER NewFilter{};
                NewFilter.DenyList.NumSeverities = _countof(Severities);
                NewFilter.DenyList.pSeverityList = Severities;
                NewFilter.DenyList.NumIDs = _countof(DenyIds);
                NewFilter.DenyList.pIDList = DenyIds;

                hr = pInfoQueue->PushStorageFilter(&NewFilter);
                CHECK_D3D_HR(hr, "Failed to push storage filter.");

                if (HasFlag(InCreateInfo.ValidationFlags,
                            D3D12_VALIDATION_FLAGS::D3D12_VALIDATION_FLAG_BREAK_ON_CORRUPTION))
                {
                    hr = pInfoQueue->SetBreakOnSeverity(D3D12_MESSAGE_SEVERITY_CORRUPTION, true);
                    CHECK_D3D_HR(hr, "Failed to set break on corruption severity.");
                }

                if (HasFlag(InCreateInfo.ValidationFlags,
                            D3D12_VALIDATION_FLAGS::D3D12_VALIDATION_FLAG_BREAK_ON_ERROR))
                {
                    hr = pInfoQueue->SetBreakOnSeverity(D3D12_MESSAGE_SEVERITY_ERROR, true);
                    CHECK_D3D_HR(hr, "Failed to set break on error severity.");
                }
            }
        }

        // verify adapter compatible with create info.
        LOG_RHI_DEBUG("[TODO]: verify adapter compatible with create info.");

        // *pOutDevice = std::make_shared<DeviceD3D12>(adapter, d3d12Device);
        auto ptr_device = MakePtr(DeviceD3D12 ,adapter, d3d12Device);



        return ptr_device;
    }
    catch (const std::runtime_error& e)
    {
        LOG_ERROR("failed to create DeviceD3D12.");
        return nullptr;
    }
}

void RHIFactoryD3D12::CreateSwapChainD3D12(IDevice* pDevice, ISwapChain** pOutSwapChain)
{
}

RHIFactoryD3D12* CreateRHIFactoryD3D12()
{
    return RHIFactoryD3D12::GetInstance();
}

ARISENRHI_END_NAMESPACE
