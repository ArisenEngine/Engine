#include "pch.h"
#include <shellapi.h>
#include "IContext.h"
#include <iostream>



IContext::IContext(int width, int height):m_Width(width),m_Height(height)
{


}

IContext::~IContext()
{

}


int IContext::Run(HINSTANCE hInstance, int nCmdShow)
{
	std::cout << " ################## Application Run ##################### " << std::endl;

	// Initialize the window class.
	WNDCLASSEX windowClass = { 0 };
	windowClass.cbSize = sizeof(WNDCLASSEX);
	windowClass.style = CS_HREDRAW | CS_VREDRAW;
	windowClass.lpfnWndProc = WindowProc;
	windowClass.hInstance = hInstance;
	windowClass.hCursor = LoadCursor(NULL, IDC_ARROW);
	windowClass.lpszClassName = L"Application";
	RegisterClassEx(&windowClass);

	RECT windowRect = { 0, 0, static_cast<LONG>(m_Width), static_cast<LONG>(m_Height) };
	AdjustWindowRect(&windowRect, WS_OVERLAPPEDWINDOW, FALSE);

	// Create the window and store a handle to it.
	auto hwnd = CreateWindow(
		windowClass.lpszClassName,
		L"Application",
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT,
		CW_USEDEFAULT,
		windowRect.right - windowRect.left,
		windowRect.bottom - windowRect.top,
		nullptr,        // We have no parent window.
		nullptr,        // We aren't using menus.
		hInstance,
		this);

	this->OnInit();

	ShowWindow(hwnd, nCmdShow);


	// Main sample loop.
	MSG msg = {};
	while (msg.message != WM_QUIT)
	{
		// Process any messages in the queue.
		if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
		{
			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}
	}

	this->OnDestroy();
	// Return this part of the WM_QUIT message to Windows.
	return static_cast<char>(msg.wParam);
}



// Main message handler for the sample.
LRESULT CALLBACK IContext::WindowProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	IContext* pEngine = reinterpret_cast<IContext*>(GetWindowLongPtr(hWnd, GWLP_USERDATA));

	switch (message)
	{
	case WM_CREATE:
	{
		// Save the EngineBase* passed in to CreateWindow.
		LPCREATESTRUCT pCreateStruct = reinterpret_cast<LPCREATESTRUCT>(lParam);
		SetWindowLongPtr(hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pCreateStruct->lpCreateParams));
	}
	return 0;

	case WM_KEYDOWN:
		if (pEngine)
		{
			pEngine->OnKeyDown(static_cast<UINT8>(wParam));
		}
		return 0;

	case WM_KEYUP:
		if (pEngine)
		{
			pEngine->OnKeyUp(static_cast<UINT8>(wParam));
		}
		return 0;

	case WM_PAINT:
		if (pEngine)
		{
			pEngine->OnUpdate();
			pEngine->OnRender();
		}
		return 0;

	case WM_SIZING:

		if (pEngine) {

			RECT* pRect = (RECT*)lParam;
			int newWidth = pRect->right - pRect->left;
			int newHeight = pRect->bottom - pRect->top;

			pEngine->m_Width = newWidth;
			pEngine->m_Height = newHeight;
			pEngine->OnResize();
		}

		return 0;

	case WM_DESTROY:
		PostQuitMessage(0);

		return 0;

	}

	// Handle any messages the switch statement didn't.
	return DefWindowProc(hWnd, message, wParam, lParam);
}