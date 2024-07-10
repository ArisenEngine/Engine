#pragma once
#include "Common/CommandHeaders.h"
#include "../EngineCommon.h"
#include "Platforms/PlatformTypes.h"
#include "Platforms/Platform.h"
#include "Platforms/RenderWindow.h"


namespace NebulaEngine::Rendering {
	
	extern "C" DLL u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height);

	inline u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height)
	{
		return Platforms::CreateRenderWindow(host, callback, width, height);
	}

	extern "C" DLL void RemoveRenderSurface(u32 id);

	inline void RemoveRenderSurface(u32 id)
	{
		Platforms::RemoveRenderSurface(id);
	}

	extern "C" DLL void ResizeRenderSurface(u32 id);

	inline void ResizeRenderSurface(u32 id)
	{
		Platforms::ResizeRenderSurface(id);
	}

	extern "C" DLL Platforms::WindowHandle GetWindowHandle(u32 id);

	inline Platforms::WindowHandle GetWindowHandle(u32 id)
	{
		return Platforms::GetWindowHandle(id);
	}
}
