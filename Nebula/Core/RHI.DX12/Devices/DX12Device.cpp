#include "DX12Device.h"
#include<iostream>

int NebulaEngine::RHI::DX12Device::GetOSVersion() const  noexcept
{
    return 110;
}

NebulaEngine::RHI::DX12Device::~DX12Device() noexcept
{
    std::cout << "~DX12Device()" << std::endl;

}

NebulaEngine::RHI::Device* CreateDevice()
{
    return new NebulaEngine::RHI::DX12Device();
}

