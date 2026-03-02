using Arisen.Native.RHI;
using System;

namespace ArisenEngine.Core.RHI
{
    public readonly struct RHIQueue
    {
        internal IntPtr Handle { get; }

        public bool IsValid => Handle != IntPtr.Zero;

        public RHIQueue(IntPtr handle)
        {
            Handle = handle;
        }

        public ulong Submit(RHICommandBuffer cb, IntPtr descriptor = default)
        {
            return RHIQueueAPI.RHIQueue_Submit(Handle, cb.RHIHandle.Index, cb.RHIHandle.Generation, descriptor);
        }

        public void Update()
        {
            RHIQueueAPI.RHIQueue_Update(Handle);
        }

        public ulong GetCompletedTicket()
        {
            return RHIQueueAPI.RHIQueue_GetCompletedTicket(Handle);
        }

        public ulong GetLatestTicket()
        {
            return RHIQueueAPI.RHIQueue_GetLatestTicket(Handle);
        }

        public void WaitForTicket(ulong ticket)
        {
            RHIQueueAPI.RHIQueue_WaitForTicket(Handle, ticket);
        }

        public RHIQueueType GetQueueType()
        {
            return (RHIQueueType)RHIQueueAPI.RHIQueue_GetType(Handle);
        }
    }
}
