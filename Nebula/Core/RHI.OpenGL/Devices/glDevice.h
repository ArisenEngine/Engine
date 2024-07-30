#pragma once


#include "../Common.h"
#include "RHI/Devices/Device.h"

namespace NebulaEngine::RHI
{
	 class glDevice final : public Device
	{
	public:

		
	};

}

extern "C" RHI_OPENGL_DLL NebulaEngine::RHI::Device * CreateDevice();


