#include "../RenderWindowAPI.h"
#include "Logger/Logger.h"
#include "Containers/Containers.h"
#include "../Window.h"
#include "../HALWindow.h"

namespace ArisenEngine::HAL
{
    using namespace ArisenEngine::Containers;

    struct RenderWindow
    {
        Window window {};
    };

    static Map<UInt32, RenderWindow> renderWindows;

    extern "C"
    {
        UInt32 CreateFullScreenRenderSurface(WindowHandle host, WindowProc callback)
        {
            // TODO
            return 0;
        }

        UInt32 CreateRenderWindow(WindowHandle host, WindowProc callback, SInt32 width, SInt32 height)
        {
            WindowInitInfo info{ callback, nullptr, host, nullptr, 0, 0, width, height };
            RenderWindow surface{ CreateNewWindow(&info) };
            ASSERT(surface.window.IsValid());
            renderWindows[surface.window.ID()] = surface;
            return surface.window.ID();
        }

        UInt32 CreateRenderWindowWithResizeCallback(WindowHandle host, WindowProc callback, WindowExitResize resizeCallback, SInt32 width, SInt32 height)
        {
            WindowInitInfo info{ callback, resizeCallback, host, nullptr, 0, 0, width, height };
            RenderWindow surface{ CreateNewWindow(&info) };
            ASSERT(surface.window.IsValid());
            renderWindows[surface.window.ID()] = surface;
            return surface.window.ID();
        }

        void RemoveRenderSurface(UInt32 id)
        {
            ASSERT(renderWindows.contains(id));
            RemoveWindow(renderWindows[id].window.ID());
        }

        void ResizeRenderSurface(UInt32 id, UInt32 width, UInt32 height)
        {
            ASSERT(renderWindows.contains(id));
            renderWindows[id].window.Resize(width, height);
        }

        WindowHandle GetWindowHandle(UInt32 id)
        {
            ASSERT(renderWindows.contains(id));
            return (WindowHandle)renderWindows[id].window.Handle();
        }

        UInt32 GetWindowWidth(UInt32 id)
        {
            ASSERT(renderWindows.contains(id));
            return renderWindows[id].window.Width();
        }

        UInt32 GetWindowHeight(UInt32 id)
        {
            ASSERT(renderWindows.contains(id));
            return renderWindows[id].window.Height();
        }

        UInt32 GetWindowId(WindowHandle handle)
        {
            return GetWindowID(handle);
        }

        void SetWindowResizeCallback(UInt32 windowId, WindowExitResize callback)
        {
            SetWindowResizeCallbackInternal(static_cast<WindowID>(windowId), callback);
        }
    }
}
