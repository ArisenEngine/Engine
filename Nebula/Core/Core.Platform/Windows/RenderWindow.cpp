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

inline NebulaEngine::u32 CreateRenderWindow(HWND host, WindowProc callback, s32 width, s32 height)
{
    Platforms::WindowInitInfo info{ callback, nullptr, host ? host : nullptr, nullptr, 0, 0, width, height };
    RenderWindow surface{ CreateNewWindow(&info) };
    ASSERT(surface.window.IsValid());
    renderWindows.emplace_back(surface);
    return (u32)renderWindows.size() - 1;
}

inline u32 CreateRenderWindowWithResizeCallback(HWND host, Platforms::WindowProc callback, WindowExitResize resizeCallback, s32 width, s32 height)
{
    Platforms::WindowInitInfo info{ callback, resizeCallback, host ? host : nullptr, nullptr, 0, 0, width, height };
    RenderWindow surface{ CreateNewWindow(&info) };
    ASSERT(surface.window.IsValid());
    renderWindows.emplace_back(surface);
    return (u32)renderWindows.size() - 1;
}

inline void RemoveRenderSurface(u32 id)
{
    ASSERT(id < renderWindows.size());
    Platforms::RemoveWindow(renderWindows[id].window.ID());
}

inline void ResizeRenderSurface(u32 id, u32 width, u32 height)
{
    ASSERT(id < renderWindows.size());
    renderWindows[id].window.Resize(width, height);
}

inline Platforms::WindowHandle GetWindowHandle(u32 id)
{
    ASSERT(id < renderWindows.size());
    return (Platforms::WindowHandle)renderWindows[id].window.Handle();
}

inline u32 GetWindowWidth(u32 id)
{
    ASSERT(id < renderWindows.size());
    return renderWindows[id].window.Width();
}

inline u32 GetWindowHeight(u32 id)
{
    ASSERT(id < renderWindows.size());
    return renderWindows[id].window.Height();
}

inline u32 GetWindowId(WindowHandle handle)
{
    return GetWindowID(handle);
}

inline void SetWindowResizeCallback(u32 windowId, WindowExitResize callback)
{
    SetWindowResizeCallbackInternal(static_cast<WindowID>(windowId), callback);
}