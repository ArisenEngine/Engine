#pragma once
#include "Windows.h"

#define WINDOES_TEST

namespace NebulaEngine::Platforms {

	struct WindowInitInfo;
	
	Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

	void RemoveWindow(WindowID id);

}