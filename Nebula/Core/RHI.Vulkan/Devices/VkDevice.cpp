#include "VkDevice.h"
#include<iostream>

int NebulaEngine::RHI::VkDevice::GetOSVersion() const  noexcept
{
    return 110;
}

NebulaEngine::RHI::VkDevice::~VkDevice() noexcept
{
    std::cout << "~VkDevice()" << std::endl;

}

NebulaEngine::RHI::Device* CreateDevice()
{
    return new NebulaEngine::RHI::VkDevice();
}

