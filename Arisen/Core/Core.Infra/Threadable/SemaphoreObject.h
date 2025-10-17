#pragma once
#include "Common/CommandHeaders.h"

namespace ArisenEngine::Threadable
{
    class SemaphoreObject
    {
    public:
        NO_COPY_NO_MOVE(SemaphoreObject)
        SemaphoreObject() = default;
        VIRTUAL_DECONSTRUCTOR(SemaphoreObject)
        virtual void* GetHandle() = 0;

        virtual void Wait() = 0;
        virtual void Signal() = 0;
        
    };
}
