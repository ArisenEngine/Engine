#pragma once
#include "Platforms/PlatformTypes.h"
#include "Platforms/Platform.h"
#include "Test.h"

using namespace NebulaEngine;

#ifdef TEST_WINDOWS

Platforms::Window _windows[4];

LRESULT WinProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
{
	switch (msg)
	{
	case WM_DESTROY:
	{
		bool allClosed{ true };
		for (u32 i{ 0 }; i < _countof(_windows); ++i)
		{
			if (!_windows[i].IsClosed())
			{
				allClosed = false;
				break;
			}
		}

		if (allClosed)
		{
			PostQuitMessage(0);
			return 0;
		}
	}

	break;
	case WM_SYSCHAR:
	{
		if (wparam == VK_RETURN && (HIWORD(lparam) & KF_ALTDOWN))
		{
			Platforms::Window win{ Platforms::WindowID {(ID::IdType)GetWindowLongPtr(hwnd, GWLP_USERDATA)} };
			win.SetFullScreen(!win.IsFullScreen());
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
		Platforms::WindowInitInfo info[]
		{
			{&WinProc, nullptr, L"Test Window 1", 100, 100, 400, 800},
			{&WinProc, nullptr, L"Test Window 2", 100, 150, 800, 400},
			{&WinProc, nullptr, L"Test Window 3", 200, 100, 400, 500},
			{&WinProc, nullptr, L"Test Window 4", 150, 100, 600, 800},
		};

		static_assert(_countof(info) == _countof(_windows));

		for (u32 i{ 0 }; i < _countof(_windows); ++i)
		{
			_windows[i] = Platforms::CreateNewWindow(&info[i]);
		}
		return true;
	}

	void Run() override
	{
		std::this_thread::sleep_for(std::chrono::microseconds(10));
	}

	void Shutdown() override
	{
		for (u32 i{ 0 }; i < _countof(_windows); ++i)
		{
			Platforms::RemoveWindow(_windows[i].ID());
		}
	}


};

#endif