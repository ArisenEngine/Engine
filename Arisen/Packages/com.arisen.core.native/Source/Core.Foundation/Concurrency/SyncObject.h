#pragma once
#include "../Base/FoundationMinimal.h"

namespace ArisenEngine::Concurrency
{
    class SyncObject
    {
    public:
        NO_COPY_NO_MOVE(SyncObject)
        SyncObject() = default;
        VIRTUAL_DECONSTRUCTOR(SyncObject)

        virtual bool Lock() = 0;
        virtual void Unlock() = 0;
        virtual void* GetHandle() = 0;
    };
}
