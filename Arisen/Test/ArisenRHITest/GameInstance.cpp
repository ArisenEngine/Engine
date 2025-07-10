#include "GameInstance.h"

#include "RHIFactoryD3D12.h"
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
        IRHIFactoryD3D12* FactoryD3D12 = CreateRHIFactoryD3D12();
        IDevice* Device;
        ISwapChain* SwapChain;
        IDeviceContext* Context;
        
        break;
    case RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_VULKAN:
        break;
    }
}

void GameInstance::LoadAssets()
{
}
