

#define TEST_WINDOWS 1
#define TEST_ENGINE 0

// #pragma comment(lib,"Core.Infra.lib")
// #pragma comment(lib,"RHI.Vulkan.lib")
#pragma comment(lib,"Engine.lib")

#include <chrono>

#include "Debugger/Logger.h"


#if(TEST_WINDOWS)

#ifdef _WIN64

#include<Windows.h>


#endif



#include "TestWindows.h"

#elif(TEST_ENGINE)

#include "TestEngine.h"

#endif



#if(TEST_WINDOWS)

#ifdef _WIN64

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)

#else

#error "should implement a main entry"

#endif

#else

int main()

#endif


{

#if _DEBUG

	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);

#endif

	EngineTest test{};


#if TEST_WINDOWS

	try
	{
		if (test.Initialize())
		{
			MSG msg{};
			bool isRunning{ true };
			while (isRunning)
			{
				while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
				{
					TranslateMessage(&msg);
					DispatchMessage(&msg);
					isRunning &= (msg.message != WM_QUIT);
				}

				test.Run();
			}
		}
	}
	catch (const std::exception &ex)
	{
		LOG_FATAL(ex.what());
	}
	
	test.Shutdown();
		


#else
	
	if (test.Initialize())
	{
		test.Run();
	}

	test.Shutdown();

#endif

	return 0;
}