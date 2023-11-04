#pragma once
#include "Common/CommandHeaders.h"
#include "../Graphics/Renderer.h"
#include"Containers\Containers.h"
#include "../Common.h"
#include "../Platforms/Windows.h"
#include "../Platforms/PlatformTypes.h"
#include "../Platforms/Platform.h"

namespace NebulaEngine::Platforms {

	using namespace Containers;

	struct WindowInitInfo;
	vector<Graphics::RenderSurface> surfaces;

#ifdef WINDOES_TEST
	
	extern "C" DLL Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

	extern "C" DLL void RemoveWindow(WindowID id);

#endif

	extern "C" DLL u32 CreateRenderSurface(HWND host, s32 width, s32 height)
	{
		assert(host);
		WindowInitInfo info{ nullptr, host, nullptr, 0, 0, width, height };
		Graphics::RenderSurface surface{ CreateNewWindow(&info), {} };
		assert(surface.window.IsValid());
		surfaces.emplace_back(surface);
		return (u32)surfaces.size() - 1;
	}

	extern "C" DLL void RemoveRenderSurface(u32 id)
	{
		assert(id < surfaces.size());
		RemoveWindow(surfaces[id].window.ID());
	}

	extern "C" DLL WindowHandle GetWindowHandle(u32 id)
	{
		assert(id < surfaces.size());
		return (WindowHandle)surfaces[id].window.Handle();
	}
}