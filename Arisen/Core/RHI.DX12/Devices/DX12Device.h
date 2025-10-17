#pragma once


#include "../Common.h"
#include "RHI/Devices/RHIDevice.h"

namespace ArisenEngine::RHI
{
	 class DX12Device final : public RHIDevice
	{
	public:

		
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::RHIDevice * CreateDevice();


