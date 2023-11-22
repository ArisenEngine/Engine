#pragma once
#include "Common/CommandHeaders.h"

namespace NebulaEngine::API
{

#ifdef _WIN64

#ifndef WIN32_MEAN_AND_LEAN
#define WIN32_MEAN_AND_LEAN
#endif

#include<Windows.h>

	using WindowProc = LRESULT(*)(HWND, UINT, WPARAM, LPARAM);
	using WindowHandle = HWND;

	struct WindowInitInfo
	{
		WindowProc         callback{ nullptr };
		WindowHandle       parent{ nullptr };
		const wchar_t*     caption{ nullptr };
		s32                left{ 0 };
		s32                top{ 0 };
		s32                width{ 1920 };
		s32                height{ 1080 };
	};

#else

#errer "platform not implement"
	// other platform 
#endif

}