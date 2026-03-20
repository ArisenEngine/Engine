#pragma once
#include "SyncObject.h"

namespace ArisenEngine::Concurrency
{
    class ScopeLock
    {
    public:
        [[nodiscard]] ScopeLock(SyncObject* InSyncObject)
            : SyncObject(InSyncObject)
        {
            assert(SyncObject);
            SyncObject->Lock();
        }

        ~ScopeLock()
        {
            Unlock();
        }

        void Unlock()
        {
            if (SyncObject)
            {
                SyncObject->Unlock();
                SyncObject = nullptr;
            }
        }

    private:
        NO_COPY_NO_DEFAULT(ScopeLock)

        // Holds the synchronization object to aggregate and scope manage.
        SyncObject* SyncObject;
    };

    class ScopeUnlock
    {
    public:
        [[nodiscard]] ScopeUnlock(SyncObject* InSyncObject)
            : SyncObject(InSyncObject)
        {
            if (InSyncObject)
            {
                InSyncObject->Unlock();
            }
        }

        ~ScopeUnlock()
        {
            if (SyncObject)
            {
                SyncObject->Lock();
            }
        }

    private:
        NO_COPY_NO_DEFAULT(ScopeUnlock)

        // Holds the synchronization object to aggregate and scope manage.
        SyncObject* SyncObject;
    };
}
