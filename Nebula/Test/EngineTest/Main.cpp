

#define TEST_WINDOWS 0
#define TEST_ENGINE 1

#if define(TEST_WINDOWS)

#ifdef _WIN64

#include<Windows.h>


#endif

#include "TestWindows.h"

#elif(TEST_ENGINE)

#include "TestEngine.h"



#endif

#pragma comment(lib,"Engine.lib")

#if define(TEST_WINDOWS)

#ifdef _WIN64

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)

#else

#error "should implement a main entry"

#endif

#else

int main()

#endif


{


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