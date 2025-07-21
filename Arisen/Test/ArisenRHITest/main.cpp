#include <Windows.h>
#include <memory>
#include <string>

#include "GameInstance.h"

std::unique_ptr<GameInstance> gInstance;

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_KEYDOWN:
        {
            if (gInstance)
            {
                gInstance->OnKeyDown(wParam);
            }
        }
        return 0;

    case WM_KEYUP:
        {
            if (gInstance)
            {
                gInstance->OnKeyUp(wParam);
            }
        }
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProc(hwnd, msg, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)
{
#if defined(_DEBUG) || defined(DEBUG)
    _CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
#endif
    const std::wstring windowTitle = L"ArisenEngine DXTest";

    // MessageBoxW(nullptr, windowTitle.c_str(), L"ArisenEngine", MB_OK);

    // 创建并初始化全局游戏实例
    gInstance = std::make_unique<GameInstance>();

    const wchar_t CLASS_NAME[] = L"DXSampleWindowClass";

    WNDCLASSW wc = {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;

    RegisterClassW(&wc);

    HWND hwnd = CreateWindowW(
        CLASS_NAME,
        windowTitle.c_str(),
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 1280, 720,
        nullptr, nullptr, hInstance, nullptr);

    if (!hwnd)
        return 0;

    ShowWindow(hwnd, nCmdShow);
    UpdateWindow(hwnd);

    gInstance->Initialize(hwnd);

    // 主消息循环
    MSG msg = {};
    while (msg.message != WM_QUIT)
    {
        if (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        else
        {
            gInstance->Loop();
        }
    }

    // 清理资源
    gInstance.reset();
    return 0;
}
