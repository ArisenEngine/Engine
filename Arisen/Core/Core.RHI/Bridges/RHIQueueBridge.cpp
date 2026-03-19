// RHIQueueBridge.cpp â€?extern "C" bridge for RHIQueue virtual methods
#include "RHI/Core/RHIDevice.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "Base/BindingMacros.h"

using namespace ArisenEngine::RHI;

ARISEN_BIND_PACKAGE("com.arisen.rhi.core")
ARISEN_BIND_BEGIN_BRIDGE("RHIQueue", "Core.RHI.dll", "Arisen.Native.RHI")

extern "C" {

RHI_DLL uint64_t RHIQueue_Submit(RHIQueue* queue, uint32_t cbIndex, uint32_t cbGeneration, RHISubmitDescriptor* descriptor)
{
    RHICommandBufferHandle handle;
    handle.index = cbIndex;
    handle.generation = cbGeneration;
    return queue->Submit(handle, descriptor);
}

RHI_DLL void RHIQueue_Update(RHIQueue* queue)
{
    queue->Update();
}

RHI_DLL uint64_t RHIQueue_GetCompletedTicket(RHIQueue* queue)
{
    return queue->GetCompletedTicket();
}

RHI_DLL uint64_t RHIQueue_GetLatestTicket(RHIQueue* queue)
{
    return queue->GetLatestTicket();
}

RHI_DLL void RHIQueue_WaitForTicket(RHIQueue* queue, uint64_t ticket)
{
    queue->WaitForTicket(ticket);
}

RHI_DLL int RHIQueue_GetType(RHIQueue* queue)
{
    return static_cast<int>(queue->GetType());
}

} // extern "C"

ARISEN_BIND_END_BRIDGE()

