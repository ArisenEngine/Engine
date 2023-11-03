#pragma once
#include "Engine/Platforms/PlatformTypes.h"
#include "Engine/Platforms/Platform.h"
#include "Test.h"

using namespace NebulaEngine;
Platforms::Window _windows[4];

class EngineTest : public Test
{
public:
	bool Initialize() override
	{
		for (u32 i{ 0 }; i < _countof(_windows); ++i)
		{
			_windows[i] = Platforms::CreateNewWindow();
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