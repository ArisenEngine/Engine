#pragma once
#include "../CoreHALCommon.h"
#include "../Common/PlatformTypes.h"

namespace ArisenEngine::HAL
{
    extern "C"
    {
        HAL_DLL UInt32 CreateFullScreenRenderSurface(WindowHandle host, WindowProc callback);
        HAL_DLL UInt32 CreateRenderWindow(WindowHandle host, WindowProc callback, SInt32 width, SInt32 height);
        HAL_DLL UInt32 CreateRenderWindowWithResizeCallback(WindowHandle host, WindowProc callback,  WindowExitResize resizeCallback, SInt32 width, SInt32 height);
        HAL_DLL void RemoveRenderSurface(UInt32 id);
        HAL_DLL void ResizeRenderSurface(UInt32 id, UInt32 width, UInt32 height);
        HAL_DLL WindowHandle GetWindowHandle(UInt32 id);
        HAL_DLL UInt32 GetWindowWidth(UInt32 id);
        HAL_DLL UInt32 GetWindowHeight(UInt32 id);
        HAL_DLL UInt32 GetWindowId(WindowHandle handle);
        HAL_DLL void SetWindowResizeCallback(UInt32 windowId, WindowExitResize callback);
        HAL_DLL void* GetWindowUserData(UInt32 windowId);
        HAL_DLL void SetWindowUserData(UInt32 windowId, void* data);
    }
}
