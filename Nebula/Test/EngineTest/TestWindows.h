#pragma once
#include "Platforms/PlatformTypes.h"
#include "Platforms/Platform.h"
#include "Test.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS


LRESULT WinProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
{
	switch (msg)
	{
	case WM_DESTROY:
	{
			PostQuitMessage(0);
	}

	break;
	case WM_SYSCHAR:
	{
		if (wparam == VK_RETURN && (HIWORD(lparam) & KF_ALTDOWN))
		{
			
			return 0;
		}
	}
	break;
	}
	return DefWindowProc(hwnd, msg, wparam, lparam);
}

class EngineTest : public Test
{
public:
	bool Initialize() override
	{
		
		return true;
	}

	void Run() override
	{
		std::this_thread::sleep_for(std::chrono::microseconds(10));
	}

	void Shutdown() override
	{
		
	}


};

#endif