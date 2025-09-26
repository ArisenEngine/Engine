#pragma once
#include <memory>
#include <windows.h>

#include "Frame.h"
#include "IDevice.h"
#include "RHITypes.h"

#include "IRenderPattern.h"
#include "IViewState.h"

using namespace ArisenRHI;

// maybe app frame should be standard in a normal style.
struct AppFrame final: public Frame
{
    // renderCommandList
    // RenderCommandSet.
};

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
    Ptr<IRenderContext> m_render_context_ptr;
    RenderPatternSettings m_screen_pass_pattern_settings;
    Ptr<IRenderPattern> m_screen_pass_pattern_ptr;
    Ptr<IViewState> m_view_state_ptr;

    std::vector<AppFrame> m_frames;
};
