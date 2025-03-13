#pragma once
#include "Platform.h"

namespace ArisenEngine::Platforms
{
    extern "C"
    {
        PLATFORM_DLL UInt32 CreateFullScreenRenderSurface(HWND host, Platforms::WindowProc callback);
        PLATFORM_DLL UInt32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, SInt32 width, SInt32 height);
        PLATFORM_DLL UInt32 CreateRenderWindowWithResizeCallback(HWND host, Platforms::WindowProc callback,  WindowExitResize resizeCallback, SInt32 width, SInt32 height);
        PLATFORM_DLL void RemoveRenderSurface(UInt32 id);
        PLATFORM_DLL void ResizeRenderSurface(UInt32 id, UInt32 width, UInt32 height);
        PLATFORM_DLL WindowHandle GetWindowHandle(UInt32 id);
        PLATFORM_DLL UInt32 GetWindowWidth(UInt32 id);
        PLATFORM_DLL UInt32 GetWindowHeight(UInt32 id);
        PLATFORM_DLL UInt32 GetWindowId(WindowHandle handle);
        PLATFORM_DLL void SetWindowResizeCallback(UInt32 windowId, WindowExitResize callback);
    }
    
}