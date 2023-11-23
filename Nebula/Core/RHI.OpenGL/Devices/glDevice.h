#pragma once


#include "../Common.h"

namespace NebulaEngine::RHI
{
	 class DLL glDevice final : public Device
	{
	public:

		int GetOSVersion() const noexcept final override;
		std::string GetPlatformName() const noexcept final override { return "OpenGL"; }
	     ~glDevice() noexcept final override;
	};

}

extern "C" DLL NebulaEngine::RHI::Device * CreateDevice();


