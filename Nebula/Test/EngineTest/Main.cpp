

#define TEST_WINDOWS 0
#define TEST_ENGINE 1

#pragma comment(lib,"Core.Infra.lib")

#include "Debugger/Logger.h"



#if(TEST_WINDOWS)

#ifdef _WIN64

#include<Windows.h>


#endif

#pragma comment(lib,"Engine.lib")
#include "API/Debugger/Logger.h"
#include "API/Renderding/RenderSurface.h"
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

	//NebulaEngine::Debugger::Logger::Log("Engine Log", "CustomThread");
	//NebulaEngine::Debugger::Logger::Info("Engine Info");
	//NebulaEngine::Debugger::Logger::Trace("Engine Trace");
	NebulaEngine::Debugger::Logger::Warning("Engine Warning Threaded");
	/*NebulaEngine::Debugger::Logger::Error("Engine Error");
	NebulaEngine::Debugger::Logger::Fatal("Engine Fatal");*/
	// testing

#if _DEBUG

	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);

#endif

	EngineTest test{};


#if TEST_WINDOWS

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