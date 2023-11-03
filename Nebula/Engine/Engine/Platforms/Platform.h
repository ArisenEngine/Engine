#pragma once
#include "../Common.h"
#include "Windows.h"

namespace NebulaEngine::Platforms {

	struct WindowInitInfo;

	extern "C" DLL Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);
	

	extern "C" DLL void RemoveWindow(WindowID id);


}