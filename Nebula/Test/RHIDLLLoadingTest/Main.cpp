
#include<iostream>
#include<windows.h>
#include "Common/CommandHeaders.h"

using namespace NebulaEngine;

int main()
{
	
	char* input = new char;
	RHI::GraphsicsAPI api;
	RHI::Device* device = nullptr;
	HMODULE rhiDll = NULL;

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
		
		DeviceCreate createDevice;

		if (rhiDll != NULL)
		{
			FreeLibrary(rhiDll);
			rhiDll = NULL;
		}

		switch (api)
		{
		case RHI::GraphsicsAPI::DirectX12:
		{
			rhiDll = LoadLibraryA("RHI.DX12.dll");
		}
		break;
		case RHI::GraphsicsAPI::Vulkan:
		{
			rhiDll = LoadLibraryA("RHI.Vulkan.dll");
		}
		break;
		case RHI::GraphsicsAPI::OpenGL:
		{
			rhiDll = LoadLibraryA("RHI.OpenGL.dll");
		}
		break;
		case RHI::GraphsicsAPI::Metal:
		{
			rhiDll = LoadLibraryA("RHI.Metal.dll");
		}
		break;
		default:
			std::cout << "Unknow platform." << std::endl;
			break;
		}


		if (rhiDll != NULL)
		{
			createDevice = (DeviceCreate)GetProcAddress(rhiDll, "CreateDevice");
			device = createDevice();
			
			if (device != nullptr)
			{
				std::cout << "API platform:" << device->GetPlatformName() << std::endl;
			}
			else
			{
				DWORD error = GetLastError();
				std::cout << "Device create failed:" << error << std::endl;
			}

		}
		else
		{
			DWORD error = GetLastError();
			std::cout << "Dll load failed:" << error << std::endl;
		}


		if (device != nullptr)
		{
			delete device;
			device = nullptr;
		}

		if (rhiDll != NULL)
		{
			FreeLibrary(rhiDll);
			rhiDll = NULL;
		}

	}

	delete input;


	return 0;
}