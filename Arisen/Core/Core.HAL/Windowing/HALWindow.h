#pragma once
#include "Window.h"
#include "../Common/PlatformTypes.h"

namespace ArisenEngine::HAL {
	
	extern "C"
	{
		HAL_DLL Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

		HAL_DLL UInt32 GetWindowID(WindowHandle handle);

		HAL_DLL void RemoveWindow(WindowID id);

		HAL_DLL void SetWindowResizeCallbackInternal(WindowID id, WindowExitResize callback);

		HAL_DLL void SetWindowResizingCallbackInternal(WindowID id, WindowResize callback);

		HAL_DLL void* GetWindowUserData(WindowID id);

		HAL_DLL void SetWindowUserData(WindowID id, void* data);
	}

}
