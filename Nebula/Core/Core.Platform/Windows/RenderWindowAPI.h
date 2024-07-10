#pragma once

#include "Platform.h"
#include "PlatformTypes.h"
#include "Windows.h"
#include "Containers/Containers.h"


namespace NebulaEngine::Platforms
{

    struct RenderWindow
    {
        Platforms::Window window {};
    };
    
    using namespace Containers;

    struct WindowInitInfo;
    extern vector<RenderWindow> renderWindows;

    inline u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height)
    {
        Platforms::WindowInitInfo info{ callback, host ? host : nullptr, nullptr, 0, 0, width, height };
        RenderWindow surface{ CreateNewWindow(&info) };
        assert(surface.window.IsValid());
        renderWindows.emplace_back(surface);
        return (u32)renderWindows.size() - 1;
    }

    inline void RemoveRenderSurface(u32 id)
    {
        assert(id < renderWindows.size());
        Platforms::RemoveWindow(renderWindows[id].window.ID());
    }

    inline void ResizeRenderSurface(u32 id)
    {
        assert(id < renderWindows.size());
        renderWindows[id].window.Resize(0, 0);
    }

    inline Platforms::WindowHandle GetWindowHandle(u32 id)
    {
        auto size = renderWindows.size();
        assert(id < renderWindows.size());
        return (Platforms::WindowHandle)renderWindows[id].window.Handle();
    }
    
}