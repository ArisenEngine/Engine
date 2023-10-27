
#include<iostream>
#include<windows.h>
#include "RHICommon/CommandHeaders.h"
//#include "./Devices/VkDevice.h"
//
//#pragma comment(lib, "RHI.Vulkan.lib")

int main()
{
	//RHI::VkDevice dev;

	//std::cout << dev.GetPlatformName() << std::endl;

	char* input = new char;
	RHI::GraphsicsAPI api;
	RHI::Device* device = nullptr;

	while (true)
	{
		std::cout << "Enter a platform code, type in 'q' to exit ..." << std::endl;
		std::cin >> *input;
		if (*input == 'q')
		{
			break;
		}

		api = RHI::GraphsicsAPI(*input - '0');

		typedef RHI::Device* (__fastcall* DeviceCreate)(void);
		HMODULE rhiDll;
		DeviceCreate createDevice;

		switch (api)
		{
		case RHI::GraphsicsAPI::DirectX12:
			std::cout << "Unimplemented platform." << std::endl;
			break;
		case RHI::GraphsicsAPI::Vulkan:
		{
			rhiDll = LoadLibraryA("RHI.Vulkan.dll");
			
			if (rhiDll != NULL)
			{
				createDevice = (DeviceCreate)GetProcAddress(rhiDll, "CreateDevice");
				device = createDevice();
				std::cout << "API platform:" << device->GetPlatformName() << std::endl;
			
			}
			else 
			{
				DWORD error = GetLastError();
				std::cout << "Dll load failed:" << error << std::endl;
			}
				
		}
			break;
		case RHI::GraphsicsAPI::OpenGL:
			std::cout << "Unimplemented platform." << std::endl;
			break;
		case RHI::GraphsicsAPI::Metal:
			std::cout << "Unimplemented platform." << std::endl;
			break;
		default:
			std::cout << "Unknow platform." << std::endl;
			break;
		}
	}

	delete input;
	delete device;

	return 0;
}