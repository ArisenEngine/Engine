#pragma once
#include "Platform.h"

namespace NebulaEngine::Platforms
{
    extern "C" PLATFORM_DLL u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height);
    extern "C" PLATFORM_DLL u32 CreateRenderWindowWithResizeCallback(HWND host, Platforms::WindowProc callback,  WindowExitResize resizeCallback, s32 width, s32 height);
    extern "C" PLATFORM_DLL void RemoveRenderSurface(u32 id);
    extern "C" PLATFORM_DLL void ResizeRenderSurface(u32 id, u32 width, u32 height);
    extern "C" PLATFORM_DLL Platforms::WindowHandle GetWindowHandle(u32 id);
    extern "C" PLATFORM_DLL u32 GetWindowWidth(u32 id);
    extern "C" PLATFORM_DLL u32 GetWindowHeight(u32 id);
    extern "C" PLATFORM_DLL u32 GetWindowId(WindowHandle handle);
    extern "C" PLATFORM_DLL void SetWindowResizeCallback(u32 windowId, WindowExitResize callback);
}