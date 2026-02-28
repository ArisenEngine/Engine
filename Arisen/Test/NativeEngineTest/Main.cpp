#define STB_IMAGE_IMPLEMENTATION 
#include "stb_image.h"
#undef STB_IMAGE_IMPLEMENTATION

#include "Framework/TestRunner.h"
#include "RHI/Rendering/RHIBasicRenderingTest.h"
#include "RHI/Rendering/RHIGPUParticleTest.h"
#include "RHI/Unit/RHISyncTest.h"
#include "RHI/Unit/RHIBindlessTest.h"
#include "RHI/Unit/RHIMultiThreadedTest.h"
#include "RHI/Unit/RHIBatchApiTest.h"
#include "RHI/Unit/RHIMemoryAliasingTest.h"
#include "RHI/Rendering/RHIMeshShaderTest.h"
#include "RHI/Rendering/RHIGeometryShaderTest.h"
#include "RHI/Rendering/RHITessellationShaderTest.h"
#include "RHI/Rendering/RHIMultiDrawIndirectTest.h"
#include "RHI/Unit/RHISecondaryCommandBufferTest.h"
#include "RHI/Unit/RHIAsyncComputeTest.h"
#include "RHI/Unit/RHIDebugTest.h"
#include "RHI/Unit/RHIInspectorTest.h"
#include "RHI/Rendering/RHIRayTracingTest.h"
#include "RHI/Rendering/RHIVRSShadingRateTest.h"
#include "../../Core/Core.HAL/Common/EngineInit.h"

#include <windows.h>
#include <vector>
#include "Base/FoundationMinimal.h"

using namespace ArisenEngine::Testing;

#ifdef _WIN32
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR lpCmdLine, int nCmdShow)
#else
int main(int argc, char** argv) 
#endif
{
#if _DEBUG
    _CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
#endif

    // Centralized Engine Initialization (Logger + Crash Handlers)
    if (!ArisenEngine::Core::EngineInit::Initialize())
    {
        return -1;
    }

    // Register Tests
    TestRunner::RegisterTest<RHIBindlessTest>();
    TestRunner::RegisterTest<RHISyncTest>();
    TestRunner::RegisterTest<RHIMultiThreadedTest>();
    TestRunner::RegisterTest<RHIBatchApiTest>();
    TestRunner::RegisterTest<RHIBasicRenderingTest>();
    TestRunner::RegisterTest<RHIGPUParticleTest>();
    TestRunner::RegisterTest<RHIMeshShaderTest>();
    TestRunner::RegisterTest<RHIGeometryShaderTest>();
    TestRunner::RegisterTest<RHITessellationShaderTest>();
    TestRunner::RegisterTest<RHIMultiDrawIndirectTest>();
    TestRunner::RegisterTest<RHISecondaryCommandBufferTest>();
    TestRunner::RegisterTest<RHIAsyncComputeTest>();
    TestRunner::RegisterTest<RHIDebugTest>();
    TestRunner::RegisterTest<RHIInspectorTest>();
    TestRunner::RegisterTest<RHIRayTracingTest>();
    TestRunner::RegisterTest<RHIVRSShadingRateTest>();
    TestRunner::RegisterTest<RHIMemoryAliasingTest>();


    // Parse simple command line for filtering (lpCmdLine for WinMain)
    ArisenEngine::String cmdLine = GetCommandLineA();

    // Run tests based on command line or run all by default
    try
    {
        if (cmdLine.Contains("--unit"))
        {
            TestRunner::RunByCategory(TestCategory::Unit);
        }
        else if (cmdLine.Contains("--rendering"))
        {
            TestRunner::RunByCategory(TestCategory::Rendering);
        }
        else
        {
            TestRunner::RunAllTests();
        }
    }
    catch (const std::exception& e)
    {
        LOG_ERRORF("Unhandled exception in main: {0}", e.what());
    }
    catch (...)
    {
        LOG_ERROR("Unknown unhandled exception in main");
    }

    // Centralized Engine Shutdown
    ArisenEngine::Core::EngineInit::Shutdown();

    return 0;
}
