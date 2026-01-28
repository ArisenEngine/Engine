#include "Windows.h"

ArisenEngine::Platforms::Window::Window(WindowID id): m_ID{id}
{
    
}

ArisenEngine::Platforms::Window::Window()
{
    
}

ArisenEngine::Platforms::WindowID ArisenEngine::Platforms::Window::ID() const
{
    return m_ID;
}