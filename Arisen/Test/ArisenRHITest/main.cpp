#include <Windows.h>

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    //DXTester* pTester = reinterpret_cast<DXTester*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));

    switch (msg)
    {
    case WM_CREATE:
        {
            // Save the DXSample* passed in to CreateWindow.
            LPCREATESTRUCT pCreateStruct = reinterpret_cast<LPCREATESTRUCT>(lParam);
            SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pCreateStruct->lpCreateParams));
        }
        return 0;

    case WM_KEYDOWN:
        // if (pTester)
        // {
        //     pTester->OnKeyDown(static_cast<UINT8>(wParam));
        // }
        return 0;

    case WM_KEYUP:
        // if (pTester)
        // {
        //     pTester->OnKeyUp(static_cast<UINT8>(wParam));
        // }
        return 0;

    case WM_PAINT:
        // if (pTester)
        // {
        //     pTester->EngineLoop();
        // }
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    // Handle any messages the switch statement didn't.
    return DefWindowProc(hwnd, msg, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow) {
    // D3D12HelloConstBuffers sample(1280, 720, L"D3D12 Hello Constant Buffers");
    // return Win32Application::Run(&sample, hInstance, nCmdShow);
    
    const LPCSTR CLASS_NAME = LPCSTR(L"DXTestWindowClass");

    WNDCLASS wc = {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;

    RegisterClass(&wc);
    
    // DXTester Tester;

    HWND hwnd = CreateWindowEx(
        0,
        CLASS_NAME,
        LPCSTR(L"ArisenEngine DXTest"),
        WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, CW_USEDEFAULT, 1280, 720,
        nullptr, nullptr, hInstance, nullptr);// &Tester);

    if (!hwnd) return 0;

    ShowWindow(hwnd, nCmdShow);

    // Tester.Initialize();

    MSG msg = {};
    while (msg.message != WM_QUIT)
    {
        if (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        } 
    }
    //Tester.ShutDown();
    return 0;
}