#pragma once
#include "IProvider.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
class ResourceProvider : public ObjectBase<IProvider>
{
public:
    static ResourceProvider& Get();
};
ARISENRHI_END_NAMESPACE