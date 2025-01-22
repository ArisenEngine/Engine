#pragma once
#include "./Windows.h"
#include "PlatformTypes.h"

#define WINDOES_TEST

namespace ArisenEngine::Platforms {

	struct WindowInitInfo;
	
	Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

	UInt32 GetWindowID(WindowHandle handle);

	void RemoveWindow(WindowID id);

	void SetWindowResizeCallbackInternal(WindowID id, WindowExitResize callback);

}