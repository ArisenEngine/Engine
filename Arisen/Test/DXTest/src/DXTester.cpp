#include "DXTester.h"

#include "Logger/Logger.h"

using namespace ArisenEngine::Debugger;

void MyMethod() {
#if defined(__clang__) || defined(__GNUC__)
    std::cout << "Function: " << __PRETTY_FUNCTION__ << std::endl;
#elif defined(_MSC_VER)
    std::cout << "Function: " << __FUNCSIG__ << std::endl;
#else
    std::cout << "Function: " << __func__ << std::endl;
#endif
}

void DXTester::Initialize()
{
    if (!Logger::GetInstance().Initialize())
    {
        throw std::exception(" Logger initialize failed.");
    }

    MyMethod();
    LOG_INFO("DXTester::Initialize done");
}

void DXTester::EngineLoop()
{
    // LOG_INFO("DXTester::EngineLoop");
    
}

void DXTester::OnKeyDown(UINT8 KeyCode)
{
}

void DXTester::OnKeyUp(UINT8 KeyCode)
{
}

void DXTester::ShutDown()
{
    LOG_INFO("DXTester::Shutdown");
    Logger::Shutdown();
}
