#pragma once
#include "Base/FoundationMinimal.h"
#include "Base/BindingMacros.h"

ARISEN_BIND_MODULE("Core.HAL.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.HAL")

namespace ArisenEngine::HAL
{

#ifdef _WIN64

#ifndef WIN32_MEAN_AND_LEAN
#define WIN32_MEAN_AND_LEAN
#endif

#include<Windows.h>

	using WindowProc = LRESULT(*)(HWND, UINT, WPARAM, LPARAM);
	using WindowExitResize = void(*)(HWND, UInt32, UInt32);
	using WindowResize = void(*)(HWND, UInt32, UInt32);
	using WindowHandle = HWND;

    ARISEN_BIND_STRUCT(WindowInitInfo)
	struct WindowInitInfo
	{
		WindowProc         callback{ nullptr };
		WindowExitResize   resizeCallback {nullptr};
		WindowResize       resizingCallback{ nullptr };
		WindowHandle       parent{ nullptr };
		const wchar_t*     caption{ nullptr };
		void*              userData{ nullptr };
		SInt32                left{ 0 };
		SInt32                top{ 0 };
		SInt32                width{ 1920 };
		SInt32                height{ 1080 };
	};

#else

    // Generic definitions for other platforms
    using WindowProc = void*;
    using WindowExitResize = void*;
    using WindowResize = void*;
    using WindowHandle = void*;

	struct WindowInitInfo
	{
		WindowProc         callback{ nullptr };
		WindowExitResize   resizeCallback {nullptr};
		WindowResize       resizingCallback{ nullptr };
		WindowHandle       parent{ nullptr };
		const wchar_t*     caption{ nullptr };
		void*              userData{ nullptr };
		SInt32                left{ 0 };
		SInt32                top{ 0 };
		SInt32                width{ 1920 };
		SInt32                height{ 1080 };
	};
#endif

}
