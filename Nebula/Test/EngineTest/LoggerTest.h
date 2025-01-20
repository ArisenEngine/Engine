#pragma once

#include "Test.h"
#include "Common/PrimitiveTypes.h"

class EngineTest : public Test
{

private :

	NebulaEngine::UInt32 m_surface_id;
	
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
