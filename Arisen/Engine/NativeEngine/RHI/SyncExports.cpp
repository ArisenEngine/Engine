#include "SyncExports.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL void RHI_Semaphore_Lock(RHI_SemaphoreHandle semaphore)
{
    auto* s = reinterpret_cast<RHI::RHISemaphore*>(semaphore);
    if (s == nullptr) return;
    s->Lock();
}

extern "C" ENGINE_DLL void RHI_Semaphore_Unlock(RHI_SemaphoreHandle semaphore)
{
    auto* s = reinterpret_cast<RHI::RHISemaphore*>(semaphore);
    if (s == nullptr) return;
    s->Unlock();
}

extern "C" ENGINE_DLL void RHI_Fence_Lock(RHI_FenceHandle fence)
{
    auto* f = reinterpret_cast<RHI::RHIFence*>(fence);
    if (f == nullptr) return;
    f->Lock();
}

extern "C" ENGINE_DLL void RHI_Fence_Unlock(RHI_FenceHandle fence)
{
    auto* f = reinterpret_cast<RHI::RHIFence*>(fence);
    if (f == nullptr) return;
    f->Unlock();
}


