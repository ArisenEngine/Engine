#include "ResourceProvider.h"

ARISENRHI_BEGIN_NAMEPSACE

ResourceProvider& ResourceProvider::Get()
{
    static ResourceProvider instance;
    return instance;
}

ARISENRHI_END_NAMESPACE