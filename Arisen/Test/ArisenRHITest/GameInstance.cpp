#include "GameInstance.h"

#include "RHIFactoryD3D12.h"
#include "SharedPtrOutWrapper.h"
#include "Containers/Containers.h"
#include "Logger/Logger.h"

GameInstance::~GameInstance()
{
}

void GameInstance::Initialize(HWND hwnd)
{
    ArisenEngine::Debugger::Logger::GetInstance().Initialize();
    LOG_DEBUG("GameInstance Init!");

    InitArisenRHI(hwnd);
    LoadAssets();
}

void GameInstance::Loop()
{
}

void GameInstance::OnKeyDown(char KeyCode)
{
}

void GameInstance::OnKeyUp(char KeyCode)
{
}

void GameInstance::InitArisenRHI(HWND hwnd)
{
    switch (DeviceType)
    {
    case RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_D3D12:
        {
            EngineCreateInfoD3D12 EngineCreateInfo;
            EngineCreateInfo.EnableValidation = true;
            EngineCreateInfo.ValidationFlags |= D3D12_VALIDATION_FLAGS::D3D12_VALIDATION_FLAG_GPU_BASED_VALIDATION;
            
            IRHIFactoryD3D12* FactoryD3D12 = CreateRHIFactoryD3D12();
            FactoryD3D12->CreateDeviceD3D12(EngineCreateInfo, &SharedPtrOutWrapper<IDevice>(pDevice));
            FactoryD3D12->CreateSwapChainD3D12(pDevice.get(),&SharedPtrOutWrapper<ISwapChain>(pSwapChain));
        }
        break;
    case RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_VULKAN:
        break;

    default:
        LOG_ERROR("ArisenRHI init failed.");
        break;
    }
}

void GameInstance::LoadAssets()
{
}
