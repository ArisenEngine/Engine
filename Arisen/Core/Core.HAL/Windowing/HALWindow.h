#pragma once
#include "Window.h"
#include "../Common/PlatformTypes.h"

namespace ArisenEngine::Platforms {
	
	Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

	UInt32 GetWindowID(WindowHandle handle);

	void RemoveWindow(WindowID id);

	void SetWindowResizeCallbackInternal(WindowID id, WindowExitResize callback);

}
