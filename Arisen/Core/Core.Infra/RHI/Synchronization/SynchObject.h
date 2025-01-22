#pragma once
#include "../../Common/CommandHeaders.h"

namespace ArisenEngine::RHI
{
    class SynchObject
    {
    public:
        NO_COPY_NO_MOVE(SynchObject)
        SynchObject() = default;
        VIRTUAL_DECONSTRUCTOR(SynchObject)
        virtual void* GetHandle() = 0;

        virtual void Lock() = 0;
        virtual void Unlock() = 0;
        
    };
}