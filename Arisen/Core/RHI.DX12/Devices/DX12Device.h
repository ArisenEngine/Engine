#pragma once


#include "../Common.h"
#include "RHI/Devices/Device.h"

namespace ArisenEngine::RHI
{
	 class DX12Device final : public Device
	{
	public:

		
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::Device * CreateDevice();


