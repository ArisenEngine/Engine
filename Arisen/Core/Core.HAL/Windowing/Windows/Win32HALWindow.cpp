#include "../HALWindow.h"
#include <cstdio>
#include "../../Common/PlatformTypes.h"
#include "Base/FoundationMinimal.h"

#ifndef UNICODE
#define UNICODE 
#endif

#ifndef _UNICODE
#define _UNICODE 
#endif


namespace ArisenEngine::HAL
{
	using namespace ArisenEngine::Containers;

#ifdef _WIN64

	namespace
	{
	#define WINDOW_PROC_CALLBACK 0
    #define WINDOW_RESIZE_CALLBACK sizeof(WindowProc)
		
		struct WindowInfo
		{
			HWND hwnd;
			RECT clientArea{ 0, 0, 1920, 1080 };
			RECT fullScreenArea{};
			POINT topLeft{ 0, 0 };
			DWORD style{ WS_VISIBLE };
			bool isFullScreen{ false };
			bool isClosed{ false };
			void* userData{ nullptr };
		};


		Vector<WindowInfo> windows;
		Vector<UInt32> availableSlots;
		
		UInt32 AddToWindows(WindowInfo info)
		{
			UInt32 id{ InvalidID };
			if (availableSlots.empty())
			{
				id = windows.size();
				windows.emplace_back(info);
			}
			else
			{
				id = availableSlots.back();
				availableSlots.pop_back();
				assert(id != u32Invalid);
				windows[id] = info;
			}

			return id;
		}

		WindowInfo* GetInfoFromId(WindowID id)
		{
			if (id < windows.size())
			{
				return &windows[id];
			}
			return nullptr;
		}

		WindowInfo* GetInfoFromHandle(WindowHandle handle)
		{
			const WindowID id{ static_cast<WindowID>(GetWindowLongPtr(handle, GWLP_USERDATA)) };
			return GetInfoFromId(id);
		}

		void RemoveFromWindows(UInt32 id)
		{
			assert(id < windows.size());
			availableSlots.emplace_back(id);

		}

		LRESULT CALLBACK InternalWindowProc(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
		{
			WindowInfo* info{ nullptr };

			bool bHasExitResizing = false;
			
			switch (msg)
			{
			case WM_NCCREATE:
			{
				LPCREATESTRUCTW createStruct = reinterpret_cast<LPCREATESTRUCTW>(lparam);
				WindowID id = static_cast<WindowID>(reinterpret_cast<uintptr_t>(createStruct->lpCreateParams));
				SetWindowLongPtr(hwnd, GWLP_USERDATA, (LONG_PTR)id);
				
				WindowInfo* winInfo = GetInfoFromId(id);
				if (winInfo)
				{
					winInfo->hwnd = hwnd;
				}
				break;
			}

			case WM_DESTROY:
			{
				WindowInfo* winInfo = GetInfoFromHandle(hwnd);
				if (winInfo) winInfo->isClosed = true;
				break;
			}
			
			case WM_MOVE:
				info = GetInfoFromHandle(hwnd);
				if (info)
				{
					info->topLeft.x = (int)(short)LOWORD(lparam);
					info->topLeft.y = (int)(short)HIWORD(lparam);
				}
				break;

			case WM_SIZE:
				if (wparam == SIZE_MINIMIZED) break;

				info = GetInfoFromHandle(hwnd);
				if (info)
				{
					const SInt32 width = LOWORD(lparam);
					const SInt32 height = HIWORD(lparam);

					if (width > 0 && height > 0)
					{
						RECT& area = info->isFullScreen ? info->fullScreenArea : info->clientArea;
						area.left = 0;
						area.top = 0;
						area.right = width;
						area.bottom = height;
						LOG_DEBUGF("[Win32HALWindow]: WM_SIZE handled. ID={0} Addr={1} FullScreen={2} Area: {3} {4} {5} {6}", 
							(unsigned)GetWindowID(hwnd), (void*)info, (int)info->isFullScreen, area.left, area.top, area.right, area.bottom);
					}
					else
					{
						LOG_DEBUGF("[Win32HALWindow]: WM_SIZE ignored due to zero dimensions ({0}x{1}).", width, height);
					}
				}
				else
				{
					LOG_DEBUGF("[Win32HALWindow]: WM_SIZE handled but info is NULL. Hwnd={0}", (void*)hwnd);
				}
				break;

			case WM_EXITSIZEMOVE:
				info = GetInfoFromHandle(hwnd);
				if (info)
				{
					GetClientRect(info->hwnd, info->isFullScreen ? &info->fullScreenArea : &info->clientArea);

					LONG_PTR longPtr{ GetWindowLongPtr(hwnd, WINDOW_RESIZE_CALLBACK) };

					if (longPtr)
					{
						auto width = info->isFullScreen ?
							info->fullScreenArea.right - info->fullScreenArea.left
							: info->clientArea.right - info->clientArea.left;
						auto height = info->isFullScreen ?
							info->fullScreenArea.bottom - info->fullScreenArea.top
							: info->clientArea.bottom - info->clientArea.top;
						((WindowExitResize)longPtr)(hwnd, width, height);
					}
				}
				break;

			case WM_SYSCOMMAND:
				if (wparam == SC_RESTORE)
				{
					info = GetInfoFromHandle(hwnd);
				}
				break;

			}
			
			LONG_PTR longPtr{ GetWindowLongPtr(hwnd, WINDOW_PROC_CALLBACK) };
			
			if (longPtr)
			{
				((WindowProc)longPtr)(hwnd, msg, wparam, lparam);
			}
			
			return DefWindowProc(hwnd, msg, wparam, lparam);
		}

		void ResizeWindow(const WindowInfo& info, const RECT& area)
		{
			RECT windowRect{ area };
			AdjustWindowRect(&windowRect, info.style, FALSE);

			const SInt32 width{ windowRect.right - windowRect.left };
			const SInt32 height{ windowRect.bottom - windowRect.top };

			MoveWindow(info.hwnd, info.topLeft.x, info.topLeft.y, width, height, true);

		}

		Math::UInt32Vector4 GetWindowSize(WindowID id)
		{
			WindowInfo* info{ GetInfoFromId(id) };
			if (info)
			{
				RECT& area{ info->isFullScreen ? info->fullScreenArea : info->clientArea };
				// LOG_DEBUGF("[Win32HALWindow]: GetWindowSize ID={0} Addr={1} FullScreen={2} Area: {3} {4} {5} {6}", 
					(unsigned)id, (void*)info, (int)info->isFullScreen, area.left, area.top, area.right, area.bottom);
				return { (UInt32)area.left, (UInt32)area.top, (UInt32)area.right, (UInt32)area.bottom };
			}
			// LOG_DEBUGF("[Win32HALWindow]: GetWindowSize ID={0} Info is NULL", (unsigned)id);
			return { 0, 0, 0, 0 };
		}

		void SetWindowCaption(WindowID id, const wchar_t* caption)
		{
			WindowInfo* info{ GetInfoFromId(id) };
			if (info)
			{
				SetWindowTextW(info->hwnd, caption);
			}
		}

		WindowHandle GetWindowHandleInternal(WindowID id)
		{
			WindowInfo* info = GetInfoFromId(id);
			return info ? info->hwnd : nullptr;
		}

	} // anonymous namespace

	// Interface of window
	extern "C"
	{
		HAL_DLL void ResizeWindow(WindowID id, UInt32 width, UInt32 height)
		{
			WindowInfo* infoPtr{ GetInfoFromId(id) };
			if (!infoPtr) return;
			WindowInfo& info = *infoPtr;

			if (info.style & WS_CHILD)
			{
				GetClientRect(info.hwnd, &info.clientArea);
			}
			else
			{
				RECT& area{ info.isFullScreen ? info.fullScreenArea : info.clientArea };

				area.bottom = area.top + height;
				area.right = area.left + width;

				ResizeWindow(info, area);

			}

		}

		HAL_DLL bool IsWindowFullScreen(WindowID id)
		{
			WindowInfo* info = GetInfoFromId(id);
			return info ? info->isFullScreen : false;
		}

		HAL_DLL void SetWindowFullScreen(WindowID id, bool isFullScreen)
		{
			WindowInfo* infoPtr{ GetInfoFromId(id) };
			if (!infoPtr) return;
			WindowInfo& info = *infoPtr;

			if (info.isFullScreen != isFullScreen)
			{
				info.isFullScreen = isFullScreen;
				if (isFullScreen)
				{
					// store the current window dimensions so they can be restored
					// when switching out of fullscreen state
					GetClientRect(info.hwnd, &info.clientArea);
					RECT rect;
					GetWindowRect(info.hwnd, &rect);
					info.topLeft.x = rect.left;
					info.topLeft.y = rect.top;
					SetWindowLongPtr(info.hwnd, GWL_STYLE, 0);
					ShowWindow(info.hwnd, SW_MAXIMIZE);

				}
				else
				{
					SetWindowLongPtr(info.hwnd, GWL_STYLE, info.style);
					ResizeWindow(info, info.clientArea);
					ShowWindow(info.hwnd, SW_NORMAL);

				}
			}

		}

		HAL_DLL Window CreateNewWindow(const WindowInitInfo* const initInfo)
		{
			WindowProc callback{ initInfo ? initInfo->callback : nullptr };
			WindowExitResize resizeCallback{ initInfo ? initInfo->resizeCallback : nullptr };
			WindowHandle parent{ initInfo ? initInfo->parent : nullptr };

			// set up window class
			WNDCLASSEXW  wc;
			ZeroMemory(&wc, sizeof(wc));
			wc.cbSize = sizeof(WNDCLASSEXW);
			wc.style = CS_HREDRAW | CS_VREDRAW;
			wc.lpfnWndProc = InternalWindowProc;
			wc.cbClsExtra = 0;
			wc.cbWndExtra = sizeof(WindowProc) + sizeof(WindowExitResize);
			wc.hInstance = 0;
			wc.hIcon = LoadIcon(NULL, IDI_APPLICATION);
			wc.hCursor = LoadCursor(NULL, IDC_ARROW);
			wc.hbrBackground = CreateSolidBrush(RGB(0, 0, 0));//CreateSolidBrush(RGB(26, 48, 76));
			wc.lpszMenuName = NULL;
			wc.lpszClassName = L"ArisenWindow";
			wc.hIconSm = LoadIcon(NULL, IDI_APPLICATION);

			// register window class 


			RegisterClassExW(&wc);

			// create an instance of window class

			WindowInfo info{ };

			info.clientArea.right = (initInfo && initInfo->width) ? info.clientArea.left + initInfo->width : info.clientArea.right;
			info.clientArea.bottom = (initInfo && initInfo->height) ? info.clientArea.top + initInfo->height : info.clientArea.bottom;
			info.style |= parent ? (WS_CHILD) : WS_OVERLAPPEDWINDOW;

			RECT rect{ info.clientArea };

			AdjustWindowRect(&rect, info.style, FALSE);

			const wchar_t* caption{ (initInfo && initInfo->caption) ? initInfo->caption : L"Arisen" };
			const SInt32 sleft{ (initInfo) ? initInfo->left : info.topLeft.x };
			const SInt32 stop{ (initInfo) ? initInfo->top : info.topLeft.y };
			const SInt32 swidth{ rect.right - rect.left };
			const SInt32 sheight{ rect.bottom - rect.top };

			const WindowID id{ AddToWindows(info) };
			
			LOG_INFOF("[Win32HALWindow]: CreateNewWindow ID={0} Initial ClientArea: {1} {2} {3} {4}", 
				(unsigned)id, info.clientArea.left, info.clientArea.top, info.clientArea.right, info.clientArea.bottom);

			info.hwnd = CreateWindowExW(
				0,
				wc.lpszClassName,
				caption,
				info.style,
				sleft,
				stop,
				swidth,
				sheight,
				parent,
				NULL,
				NULL,
				(LPVOID)(uintptr_t)id
			);

			if (info.hwnd)
			{
				if (callback) SetWindowLongPtr(info.hwnd, WINDOW_PROC_CALLBACK, (LONG_PTR)callback);

				if (resizeCallback) SetWindowLongPtr(info.hwnd, WINDOW_RESIZE_CALLBACK, (LONG_PTR)resizeCallback);

				assert(GetLastError() == 0);

				ShowWindow(info.hwnd, SW_NORMAL);
				UpdateWindow(info.hwnd);
				return Window{ id };
			}

			return {};
		}


		HAL_DLL void RemoveWindow(WindowID id)
		{
			WindowInfo* info{ GetInfoFromId(id) };
			if (info)
			{
				DestroyWindow(info->hwnd);
				RemoveFromWindows(id);
			}
		}

		HAL_DLL UInt32 GetWindowID(WindowHandle handle)
		{
			const WindowID id{ static_cast<WindowID>(GetWindowLongPtr(handle, GWLP_USERDATA)) };
			return id;
		}

		HAL_DLL void SetWindowResizeCallbackInternal(WindowID id, WindowExitResize callback)
		{
			WindowInfo* info{ GetInfoFromId(id) };
			if (info)
			{
				SetWindowLongPtr(info->hwnd, WINDOW_RESIZE_CALLBACK, (LONG_PTR)callback);
			}
		}

		HAL_DLL void* GetWindowUserData(WindowID id)
		{
			WindowInfo* info = GetInfoFromId(id);
			return info ? info->userData : nullptr;
		}

		HAL_DLL void SetWindowUserData(WindowID id, void* data)
		{
			WindowInfo* info = GetInfoFromId(id);
			if (info) info->userData = data;
		}

		HAL_DLL bool IsWindowClosed(WindowID id)
		{
			WindowInfo* info = GetInfoFromId(id);
			return info ? info->isClosed : true;
		}
	}
	
#else

#error "platform not be implement"

#endif


	bool Window::IsValid() const
	{
		return m_ID != InvalidID;
	}

	void Window::SetFullScreen(bool isFullScreen) const
	{
		assert(IsValid());
		SetWindowFullScreen(m_ID, isFullScreen);
	}

	bool Window::IsFullScreen() const
	{
		assert(IsValid());
		return IsWindowFullScreen(m_ID);
	}

	void* Window::Handle() const
	{
		assert(IsValid());
		return GetWindowHandleInternal(m_ID);
	}

	void Window::SetCaption(const wchar_t* caption) const
	{
		assert(IsValid());
		SetWindowCaption(m_ID, caption);
	}

	 Math::UInt32Vector4 Window::Size() const
	{
		assert(IsValid());
		return GetWindowSize(m_ID);
	}

	void Window::Resize(UInt32 width, UInt32 height) const
	{
		assert(IsValid());
		ResizeWindow(m_ID, width, height);
	}

	 UInt32 Window::Width() const
	{
		assert(IsValid());
		Math::UInt32Vector4 s{ Size() };
		return s.z - s.x;
	}

	 UInt32 Window::Height() const
	{
		assert(IsValid());
		Math::UInt32Vector4 s{ Size() };
		return s.w - s.y;
	}

	bool Window::IsClosed() const
	{
		assert(IsValid());
		return IsWindowClosed(m_ID);
	}

	void* Window::GetUserData() const
	{
		assert(IsValid());
		return GetWindowUserData(m_ID);
	}

	void Window::SetUserData(void* data) const
	{
		assert(IsValid());
		SetWindowUserData(m_ID, data);
	}
	
}
