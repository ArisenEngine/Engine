#pragma once

#include "Common\CommandHeaders.h"
#include "RHI\Surfaces\Surface.h"
#include "../Platforms/Windows.h"

namespace NebulaEngine::Graphics
{
	using namespace API;
	using namespace RHI;

	struct RenderSurface
	{
		Window window {};
		Surface surface {};
	};
}