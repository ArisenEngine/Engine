#pragma once
#include <stdexcept>
#include <basetsd.h>
#include <windows.h>

#include "Common.h"


 class DLL IContext
{
public:

	IContext(int width, int height);
	~IContext();

	virtual void OnInit() = 0;
	virtual void OnUpdate() = 0;
	virtual void OnRender() = 0;
	virtual void OnDestroy() = 0;
	virtual void OnResize() = 0;

	// Samples override the event handlers to handle specific messages.
	virtual void OnKeyDown(UINT8 /*key*/) {}
	virtual void OnKeyUp(UINT8 /*key*/) {}

	int Run(HINSTANCE hInstance, int nCmdShow);

protected:

	static LRESULT CALLBACK WindowProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
	
	int m_Width;
	int m_Height;

private:


};

