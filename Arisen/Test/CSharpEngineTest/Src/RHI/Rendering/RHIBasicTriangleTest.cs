using System;
using Arisen.Native.RHI;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.RHI.Rendering
{
    public class RHIBasicTriangleTest : RHIRenderingTestBase
    {
        public override string GetName() => "BasicTriangleTest";

        protected override bool SetupTest()
        {
            base.SetupTest();
            Logger.Log("Setting up RHIBasicTriangleTest");
            return true;
        }

        protected override void RenderFrame()
        {
            // For the first run, let's just log and clear the screen if we can get the swapchain working
            // Since we need to implement more of RHIRenderingTestBase (CmdPool, Swapchain creation), 
            // the first goal is to see the window and the loop running.
            
            // Console.WriteLine($"Rendering frame {_frameIndex}");
        }

        protected override void TeardownTest()
        {
            Logger.Log("Tearing down RHIBasicTriangleTest");
            base.TeardownTest();
        }
    }
}
