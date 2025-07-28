#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
bool ObjectBase::SetName(std::string_view name)
{
    if (m_name == name)
    {
        return false;
    }

    // TODO : name changed.

    m_name = name;
    return true;
}

ARISENRHI_END_NAMESPACE
