#pragma once


#include "../Common.h"

namespace NebulaEngine::RHI
{
	 class DLL VkDevice final : public Device
	{
	public:

		int GetOSVersion() const noexcept final override;
		std::string GetPlatformName() const noexcept final override { return "Vulkan"; }
	     ~VkDevice() noexcept final override;
	};

}

extern "C" DLL NebulaEngine::RHI::Device * CreateDevice();


