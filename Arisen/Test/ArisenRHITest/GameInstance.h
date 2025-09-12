#pragma once
#include <memory>
#include <windows.h>

#include "IDevice.h"
#include "ISwapChain.h"
#include "RHITypes.h"
#include <format>

#include "IRenderPattern.h"

using namespace ArisenRHI;

class GameInstance
{
public:
    ~GameInstance();
    
    void Initialize(HWND hwnd);
    void Loop();

    void OnKeyDown(char KeyCode);
    void OnKeyUp(char KeyCode);

private:
    void InitArisenRHI(HWND hwnd);
    void LoadAssets();
    
private:
    RHI_DEVICE_TYPE DeviceType{RHI_DEVICE_TYPE::RHI_DEVICE_TYPE_D3D12};

    Ptr<IDevice> pDevice;
    Ptr<IRenderContext> pRender_context;
    RenderPatternSettings m_screen_pass_pattern_settings;
};
