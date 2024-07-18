#pragma once
#include "Logger/Logger.h"
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
    static Vector<RenderWindow> renderWindows;

    extern "C" PLATFORM_DLL u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height);
    inline u32 CreateRenderWindow(HWND host, Platforms::WindowProc callback, s32 width, s32 height)
    {
        Platforms::WindowInitInfo info{ callback, host ? host : nullptr, nullptr, 0, 0, width, height };
        RenderWindow surface{ CreateNewWindow(&info) };
        ASSERT(surface.window.IsValid());
        renderWindows.emplace_back(surface);
        return (u32)renderWindows.size() - 1;
    }

    extern "C" PLATFORM_DLL void RemoveRenderSurface(u32 id);
    inline void RemoveRenderSurface(u32 id)
    {
        ASSERT(id < renderWindows.size());
        Platforms::RemoveWindow(renderWindows[id].window.ID());
    }

    extern "C" PLATFORM_DLL void ResizeRenderSurface(u32 id);
    inline void ResizeRenderSurface(u32 id)
    {
        ASSERT(id < renderWindows.size());
        renderWindows[id].window.Resize(0, 0);
    }

    extern "C" PLATFORM_DLL Platforms::WindowHandle GetWindowHandle(u32 id);
    inline Platforms::WindowHandle GetWindowHandle(u32 id)
    {
        ASSERT(id < renderWindows.size());
        return (Platforms::WindowHandle)renderWindows[id].window.Handle();
    }

    extern "C" PLATFORM_DLL u32 GetWindowWidth(u32 id);
    inline u32 GetWindowWidth(u32 id)
    {
        ASSERT(id < renderWindows.size());
        return renderWindows[id].window.Width();
    }

    extern "C" PLATFORM_DLL u32 GetWindowHeight(u32 id);
    inline u32 GetWindowHeight(u32 id)
    {
        ASSERT(id < renderWindows.size());
        return renderWindows[id].window.Height();
    }
    
}