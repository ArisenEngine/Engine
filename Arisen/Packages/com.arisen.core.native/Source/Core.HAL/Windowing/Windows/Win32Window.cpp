#include "../Window.h"
#include "../../Common/PlatformTypes.h"

namespace ArisenEngine::HAL
{
    Window::Window(WindowID id) : m_ID{id}
    {
    }

    Window::Window()
    {
    }

    WindowID Window::ID() const
    {
        return m_ID;
    }

    // TODO: Implement other members using Win32 API
}
