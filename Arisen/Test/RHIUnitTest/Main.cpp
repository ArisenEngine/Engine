#define TEST_WINDOWS 1

#include "Framework/TestRunner.h"
#include "RHI/RHIBasicRenderingTest.h"
#include "../../Engine/NativeEngine/Core/EngineInit.h"
#include <windows.h>

using namespace ArisenEngine::Testing;

#if(TEST_WINDOWS)
#ifdef _WIN64
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)
#else
// Ensure a main entry exists for other configurations if needed
int main() 
#endif
#else
int main()
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

    // Register the Basic Rendering Test
    TestRunner::RegisterTest<RHIBasicRenderingTest>();

    // Run all registered tests
    TestRunner::RunAllTests();

    // Centralized Engine Shutdown
    ArisenEngine::Core::EngineInit::Shutdown();

    return 0;
}