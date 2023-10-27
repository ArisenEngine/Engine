#include "VkDevice.h"
#include<iostream>

int RHI::VkDevice::GetOSVersion() const  noexcept
{
    return 110;
}

RHI::VkDevice::~VkDevice() noexcept
{
    std::cout << "~VkDevice()" << std::endl;

}

RHI::Device* CreateDevice()
{
    return new RHI::VkDevice();
}

