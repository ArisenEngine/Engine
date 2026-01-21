#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"
#undef STB_IMAGE_IMPLEMENTATION

#include "Framework/TestRunner.h"
#include "RHI/RHIBasicRenderingTest.h"
#include "RHI/RHIDynamicRenderingTest.h"
#include "RHI/Unit/RHISyncTest.h"
#include "RHI/Unit/RHIBindlessTest.h"
#include "../../Engine/NativeEngine/Core/EngineInit.h"
#include <windows.h>
#include <vector>
#include <string>

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
    TestRunner::RegisterTest<RHIBasicRenderingTest>();
    TestRunner::RegisterTest<RHIDynamicRenderingTest>();

    // Parse simple command line for filtering (lpCmdLine for WinMain)
    std::string cmdLine = GetCommandLineA();
    
    // Run tests based on command line or run all by default
    if (cmdLine.find("--unit") != std::string::npos)
    {
        TestRunner::RunByCategory(TestCategory::Unit);
    }
    else if (cmdLine.find("--rendering") != std::string::npos)
    {
        TestRunner::RunByCategory(TestCategory::Rendering);
    }
    else
    {
        // Run everything or specific default
        TestRunner::RunAllTests();
    }

    // Centralized Engine Shutdown
    ArisenEngine::Core::EngineInit::Shutdown();

    return 0;
}