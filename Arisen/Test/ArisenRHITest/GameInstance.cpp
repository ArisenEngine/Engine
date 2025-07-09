#include "GameInstance.h"

#include "Containers/Containers.h"
#include "Logger/Logger.h"

GameInstance::~GameInstance()
{
}

void GameInstance::Initialize()
{
    ArisenEngine::Debugger::Logger::GetInstance().Initialize();
    LOG_DEBUG("Initi GameINstance");
}

void GameInstance::Loop()
{
    LOG_DEBUG("Loop");
    
}

void GameInstance::OnKeyDown(char KeyCode)
{
}

void GameInstance::OnKeyUp(char KeyCode)
{
}
