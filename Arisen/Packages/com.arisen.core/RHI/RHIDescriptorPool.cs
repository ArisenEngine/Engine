using System;
using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI
{
    public readonly struct RHIDescriptorPool
    {
        internal IntPtr NativePtr { get; }

        internal RHIDescriptorPool(IntPtr nativePtr)
        {
            NativePtr = nativePtr;
        }

        public bool IsValid => NativePtr != IntPtr.Zero;

        public unsafe uint AddPool(EDescriptorType[] types, uint[] counts, uint maxSets)
        {
            int[] iTypes = new int[types.Length];
            for (int i = 0; i < types.Length; i++) iTypes[i] = (int)types[i];

            fixed (int* pTypes = iTypes)
            fixed (uint* pCounts = counts)
            {
                return RHIDescriptorPoolAPI.RHIDescriptorPool_AddPool(NativePtr, (IntPtr)pTypes, (IntPtr)pCounts, (uint)types.Length, maxSets);
            }
        }

        public bool ResetPool(uint poolId)
        {
            return RHIDescriptorPoolAPI.RHIDescriptorPool_ResetPool(NativePtr, poolId);
        }

        public uint AllocDescriptorSet(uint poolId, uint layoutIndex, RHIPipelineState pso)
        {
            return RHIDescriptorPoolAPI.RHIDescriptorPool_AllocDescriptorSet(NativePtr, poolId, layoutIndex, pso.NativePtr);
        }

        public void UpdateDescriptorSet(uint poolId, uint setIndex, RHIPipelineState pso)
        {
            RHIDescriptorPoolAPI.RHIDescriptorPool_UpdateDescriptorSet(NativePtr, poolId, setIndex, pso.NativePtr);
        }
    }
}
