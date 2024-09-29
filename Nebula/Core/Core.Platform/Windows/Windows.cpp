#include "Windows.h"

NebulaEngine::Platforms::Window::Window(WindowID id): m_ID{id}
{
    
}

NebulaEngine::Platforms::Window::Window()
{
    
}

NebulaEngine::Platforms::WindowID NebulaEngine::Platforms::Window::ID() const
{
    return m_ID;
}