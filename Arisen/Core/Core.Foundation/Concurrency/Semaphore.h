#pragma once
#include "../Base/FoundationMinimal.h"

namespace ArisenEngine::Concurrency
{
    class Semaphore
    {
    public:
        NO_COPY_NO_MOVE(Semaphore)
        Semaphore() = default;
        VIRTUAL_DECONSTRUCTOR(Semaphore)
        virtual void* GetHandle() = 0;

        virtual void Wait() = 0;
        virtual void Signal() = 0;
    };
}
