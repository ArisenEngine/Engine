#pragma once

#include "Common/CommandHeaders.h"
#include "RHI/Surfaces/Surface.h"
#include "../Platforms/Windows.h"
#include "../Renderding/RenderSurface.h"

namespace NebulaEngine::Rendering
{
	using namespace RHI;

	struct RenderSurface
	{
		Platforms::Window window {};
	};
}
