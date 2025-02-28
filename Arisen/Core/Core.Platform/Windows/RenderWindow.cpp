#include "./RenderWindowAPI.h"
#include "Logger/Logger.h"
#include "Containers/Containers.h"
#include "Windows.h"

using namespace ArisenEngine::Containers;
using namespace ArisenEngine;
using namespace ArisenEngine::Platforms;

struct RenderWindow
{
    Platforms::Window window {};
};

static Map<UInt32, RenderWindow> renderWindows;

UInt32 Platforms::CreateFullScreenRenderSurface(HWND host, Platforms::WindowProc callback)
{
    // TODO
    return 0;
}

inline UInt32 Platforms::CreateRenderWindow(HWND host, WindowProc callback, SInt32 width, SInt32 height)
{
    Platforms::WindowInitInfo info{ callback, nullptr, host ? host : nullptr, nullptr, 0, 0, width, height };
    RenderWindow surface{ CreateNewWindow(&info) };
    ASSERT(surface.window.IsValid());
    renderWindows[surface.window.ID()] = surface;
    return surface.window.ID();
}

inline UInt32 Platforms::CreateRenderWindowWithResizeCallback(HWND host, Platforms::WindowProc callback, WindowExitResize resizeCallback, SInt32 width, SInt32 height)
{
    Platforms::WindowInitInfo info{ callback, resizeCallback, host ? host : nullptr, nullptr, 0, 0, width, height };
    RenderWindow surface{ CreateNewWindow(&info) };
    ASSERT(surface.window.IsValid());
    renderWindows[surface.window.ID()] = surface;
    return surface.window.ID();
}

inline void Platforms::RemoveRenderSurface(UInt32 id)
{
    ASSERT(renderWindows.contains(id));
    Platforms::RemoveWindow(renderWindows[id].window.ID());
}

inline void Platforms::ResizeRenderSurface(UInt32 id, UInt32 width, UInt32 height)
{
    ASSERT(renderWindows.contains(id));
    renderWindows[id].window.Resize(width, height);
}

inline Platforms::WindowHandle Platforms::GetWindowHandle(UInt32 id)
{
    ASSERT(renderWindows.contains(id));
    return (Platforms::WindowHandle)renderWindows[id].window.Handle();
}

inline UInt32 Platforms::GetWindowWidth(UInt32 id)
{
    ASSERT(renderWindows.contains(id));
    return renderWindows[id].window.Width();
}

inline UInt32 Platforms::GetWindowHeight(UInt32 id)
{
    ASSERT(renderWindows.contains(id));
    return renderWindows[id].window.Height();
}

inline UInt32 Platforms::GetWindowId(WindowHandle handle)
{
    return GetWindowID(handle);
}

inline void Platforms::SetWindowResizeCallback(UInt32 windowId, WindowExitResize callback)
{
    SetWindowResizeCallbackInternal(static_cast<WindowID>(windowId), callback);
}