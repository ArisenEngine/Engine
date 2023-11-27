#pragma once
#include "Common/CommandHeaders.h"
#include "../../EngineCommon.h"
#include "../../Platforms/PlatformTypes.h"
#include "../../Platforms/Platform.h"
#include "../../Graphics/Renderer.h"



namespace NebulaEngine::Rendering {

	using namespace Containers;

	struct WindowInitInfo;
	vector<Graphics::RenderSurface> surfaces;
	
	extern "C" DLL u32 CreateRenderSurface(HWND host, WindowProc callback, s32 width, s32 height);

	inline u32 CreateRenderSurface(HWND host, WindowProc callback, s32 width, s32 height)
	{
		WindowInitInfo info{ callback, host ? host : nullptr, nullptr, 0, 0, width, height };
		Graphics::RenderSurface surface{ CreateNewWindow(&info), {} };
		assert(surface.window.IsValid());
		surfaces.emplace_back(surface);
		return (u32)surfaces.size() - 1;
	}

	extern "C" DLL void RemoveRenderSurface(u32 id);

	inline void RemoveRenderSurface(u32 id)
	{
		assert(id < surfaces.size());
		RemoveWindow(surfaces[id].window.ID());
	}

	extern "C" DLL void ResizeRenderSurface(u32 id);

	inline void ResizeRenderSurface(u32 id)
	{
		assert(id < surfaces.size());
		surfaces[id].window.Resize(0, 0);
	}

	extern "C" DLL WindowHandle GetWindowHandle(u32 id);

	inline WindowHandle GetWindowHandle(u32 id)
	{
		assert(id < surfaces.size());
		return (WindowHandle)surfaces[id].window.Handle();
	}
}
