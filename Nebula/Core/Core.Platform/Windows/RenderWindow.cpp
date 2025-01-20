#include "./RenderWindowAPI.h"
#include "Logger/Logger.h"
#include "Containers/Containers.h"
#include "Windows.h"

using namespace NebulaEngine::Containers;
using namespace NebulaEngine;
using namespace NebulaEngine::Platforms;

struct RenderWindow
{
    Platforms::Window window {};
};

static Vector<RenderWindow> renderWindows;

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
    renderWindows.emplace_back(surface);
    return (UInt32)renderWindows.size() - 1;
}

inline UInt32 Platforms::CreateRenderWindowWithResizeCallback(HWND host, Platforms::WindowProc callback, WindowExitResize resizeCallback, SInt32 width, SInt32 height)
{
    Platforms::WindowInitInfo info{ callback, resizeCallback, host ? host : nullptr, nullptr, 0, 0, width, height };
    RenderWindow surface{ CreateNewWindow(&info) };
    ASSERT(surface.window.IsValid());
    renderWindows.emplace_back(surface);
    return (UInt32)renderWindows.size() - 1;
}

inline void Platforms::RemoveRenderSurface(UInt32 id)
{
    ASSERT(id < renderWindows.size());
    Platforms::RemoveWindow(renderWindows[id].window.ID());
}

inline void Platforms::ResizeRenderSurface(UInt32 id, UInt32 width, UInt32 height)
{
    ASSERT(id < renderWindows.size());
    renderWindows[id].window.Resize(width, height);
}

inline Platforms::WindowHandle Platforms::GetWindowHandle(UInt32 id)
{
    ASSERT(id < renderWindows.size());
    return (Platforms::WindowHandle)renderWindows[id].window.Handle();
}

inline UInt32 Platforms::GetWindowWidth(UInt32 id)
{
    ASSERT(id < renderWindows.size());
    return renderWindows[id].window.Width();
}

inline UInt32 Platforms::GetWindowHeight(UInt32 id)
{
    ASSERT(id < renderWindows.size());
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