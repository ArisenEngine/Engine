#pragma once

#include "Test.h"

class EngineTest : public Test
{

public:
	bool Initialize() override
	{
		return NebulaEngine::Debugger::Logger::Initialize();
	}

	void Run() override
	{
		// std::this_thread::sleep_for(std::chrono::microseconds(10));
		NebulaEngine::Debugger::Logger::Warning("Engine Warning Threaded");
	}

	void Shutdown() override
	{
		NebulaEngine::Debugger::Logger::Exit();
	}


};