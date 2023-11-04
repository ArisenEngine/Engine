#pragma once
#include "Windows.h"

namespace NebulaEngine::Platforms {

#ifndef WINDOES_TEST

	struct WindowInitInfo;
	
	Window CreateNewWindow(const WindowInitInfo* const initInfo = nullptr);

	void RemoveWindow(WindowID id);

#endif

}