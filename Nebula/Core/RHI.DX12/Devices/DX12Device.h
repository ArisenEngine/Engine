#pragma once

#include "./Common/CommandHeaders.h"
#include "../Common.h"

namespace NebulaEngine::RHI
{
	 class DLL DX12Device final : public Device
	{
	public:

		int GetOSVersion() const noexcept final override;
		std::string GetPlatformName() const noexcept final override { return "DX12"; }
	     ~DX12Device() noexcept final override;
	};

}

extern "C" DLL NebulaEngine::RHI::Device * CreateDevice();


