#include "glDevice.h"
#include<iostream>

int NebulaEngine::RHI::glDevice::GetOSVersion() const  noexcept
{
    return 110;
}

NebulaEngine::RHI::glDevice::~glDevice() noexcept
{
    std::cout << "~glDevice()" << std::endl;

}

NebulaEngine::RHI::Device* CreateDevice()
{
    return new NebulaEngine::RHI::glDevice();
}

