#pragma once

#include "Test.h"

class EngineTest : public Test
{

public:
	bool Initialize() override
	{
		
		return true;
	}

	void Run() override
	{
		std::this_thread::sleep_for(std::chrono::microseconds(10));
	}

	void Shutdown() override
	{
		NebulaEngine::Debugger::Logger::Exit();
	}


};