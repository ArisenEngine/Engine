using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI
{
    /// <summary>
    /// Provides zero-overhead extension methods for injecting GPU debug markers.
    /// </summary>
    public static class RHICommandBufferExtensions
    {
        private static readonly float[] DefaultColor = { 0.8f, 0.8f, 0.8f, 1.0f };

        /// <summary>
        /// Begins a GPU debug marker zone. Use with a `using` statement.
        /// When ARISEN_PROFILER_ENABLED is not defined, this becomes a zero-overhead no-op.
        /// </summary>
        /// <param name="cb">The command buffer.</param>
        /// <param name="name">The name of the debug region.</param>
        /// <param name="color">Optional RGBA color array (0.0 to 1.0).</param>
        /// <returns>A disposable marker context.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RHICommandBufferDebugMarker BeginDebugMarker(this RHICommandBuffer cb, string name, float[] color = null)
        {
#if ARISEN_PROFILER_ENABLED
            RHICommandBufferAPI.RHICommandBuffer_BeginDebugLabel(cb.NativePtr, name, color ?? DefaultColor);
            return new RHICommandBufferDebugMarker(cb);
#else
            return default;
#endif
        }

        /// <summary>
        /// Inserts a single, instantaneous GPU debug marker.
        /// When ARISEN_PROFILER_ENABLED is not defined, this becomes a zero-overhead no-op.
        /// </summary>
        /// <param name="cb">The command buffer.</param>
        /// <param name="name">The name of the marker.</param>
        /// <param name="color">Optional RGBA color array (0.0 to 1.0).</param>
        [Conditional("ARISEN_PROFILER_ENABLED")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertDebugMarker(this RHICommandBuffer cb, string name, float[] color = null)
        {
            RHICommandBufferAPI.RHICommandBuffer_InsertDebugMarker(cb.NativePtr, name, color ?? DefaultColor);
        }
    }

    /// <summary>
    /// A disposable handle for an RHI GPU debug marker zone.
    /// </summary>
    public readonly struct RHICommandBufferDebugMarker : IDisposable
    {
#if ARISEN_PROFILER_ENABLED
        private readonly RHICommandBuffer _cb;

        internal RHICommandBufferDebugMarker(RHICommandBuffer cb)
        {
            _cb = cb;
        }
#endif

        /// <summary>
        /// Ends the debug marker zone.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if ARISEN_PROFILER_ENABLED
            if (_cb.IsValid)
            {
                RHICommandBufferAPI.RHICommandBuffer_EndDebugLabel(_cb.NativePtr);
            }
#endif
        }
    }
}
