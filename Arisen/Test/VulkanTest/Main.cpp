

#define TEST_WINDOWS 1
#define TEST_ENGINE 0

#include <chrono>

#include "Logger/Logger.h"
#include <csignal>
#include <exception>


#if(TEST_WINDOWS)

#ifdef _WIN64

#include<Windows.h>

// Ensure logger shutdown on unhandled SEH
static LONG WINAPI ArisenUnhandledExceptionFilter(EXCEPTION_POINTERS*)
{
    try { ArisenEngine::Debugger::Logger::Shutdown(); } catch(...) {}
    return EXCEPTION_EXECUTE_HANDLER;
}


#endif



#if TEST_WINDOWS

// Ensure logger shutdown on abnormal termination
static void ArisenOnTerminate()
{
    try { ArisenEngine::Debugger::Logger::Shutdown(); } catch(...) {}
    std::abort();
}

static void ArisenOnSignal(int)
{
    try { ArisenEngine::Debugger::Logger::Shutdown(); } catch(...) {}
#ifdef _WIN64
    ::ExitProcess(3);
#else
    std::_Exit(3);
#endif
}

#endif
#include "VulkanTest.h"

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

	// Crash-safe shutdown hooks
#if TEST_WINDOWS
#ifdef _WIN64
	SetUnhandledExceptionFilter(ArisenUnhandledExceptionFilter);
#endif
	std::set_terminate(ArisenOnTerminate);
	signal(SIGABRT, ArisenOnSignal);
	signal(SIGSEGV, ArisenOnSignal);
	signal(SIGILL, ArisenOnSignal);
	signal(SIGFPE, ArisenOnSignal);
	std::atexit([](){
		try { ArisenEngine::Debugger::Logger::Shutdown(); } catch(...) {}
	});
#endif

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
	catch (...)
	{
		LOG_FATAL("Unhandled exception");
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