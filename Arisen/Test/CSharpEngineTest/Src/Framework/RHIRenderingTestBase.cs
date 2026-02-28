using System;
using System.Collections.Generic;
using Arisen.Native.RHI;
using Arisen.Native.HAL;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.RHI;

namespace CSharpEngineTest.Framework
{
    public abstract class RHIRenderingTestBase : RHITestBase
    {
        protected RHICommandBufferPool? _cmdPool;
        // protected RHIDescriptorPool _descriptorPool;
        protected RHISwapChain? _swapChain;

        public override TestCategory GetCategory() => TestCategory.Rendering;

        protected override bool SetupTest()
        {
            _swapChain = _device?.GetSurface().GetSwapChain();
            InitCommonResources();
            return true;
        }

        protected virtual void InitCommonResources()
        {
            if (_device == null) return;

            var factory = _device.Value.GetFactory();
            _cmdPool = factory.CreateCommandBufferPool(Arisen.Native.RHI.RHIQueueType.Graphics);
        }

        protected override void TeardownTest()
        {
            if (_cmdPool != null)
            {
                _cmdPool = null;
            }
        }
    }
}
