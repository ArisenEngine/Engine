#pragma once
#include "windows.h"

class DXTester
{
public:
    void Initialize();
    void EngineLoop();

    void OnKeyDown(UINT8 KeyCode);
    void OnKeyUp(UINT8 KeyCode);
    void ShutDown();
};
