#include "Platform.h"
#include "PlatformTypes.h"
#include "Common/CommandHeaders.h"
#include "Windows.h"

namespace NebulaEngine::Platforms
{
	using namespace NebulaEngine::Containers;

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
		};


		Vector<WindowInfo> windows;
		Vector<u32> availableSlots;
		
		u32 AddToWindows(WindowInfo info)
		{
			u32 id{ InvalidID };
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

		WindowInfo& GetInfoFromId(WindowID id)
		{
			assert(id < windows.size());
			assert(windows[id].hwnd);
			return windows[id];
		}

		WindowInfo& GetInfoFromHandle(WindowHandle handle)
		{
			const WindowID id{ static_cast<WindowID>(GetWindowLongPtr(handle, GWLP_USERDATA))};
			return GetInfoFromId(id);
		}

		void RemoveFromWindows(u32 id)
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
			case WM_DESTROY:
				GetInfoFromHandle(hwnd).isClosed = true;
				break;
			
			case WM_EXITSIZEMOVE:
				info = &GetInfoFromHandle(hwnd);
				bHasExitResizing = true;
				break;

			case WM_SIZE:
				if (wparam == SIZE_MAXIMIZED)
				{
					info = &GetInfoFromHandle(hwnd);
				}
				break;
			case WM_SYSCOMMAND:
				if (wparam == SC_RESTORE)
				{
					info = &GetInfoFromHandle(hwnd);
				}
				break;

			}

			if (info)
			{
				assert(info->hwnd);
				GetClientRect(info->hwnd, info->isFullScreen ? &info->fullScreenArea : &info->clientArea);

				if (bHasExitResizing)
				{
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
			}
			
			LONG_PTR longPtr{ GetWindowLongPtr(hwnd, WINDOW_PROC_CALLBACK) };
			
			if (longPtr)
			{
				return ((WindowProc)longPtr)(hwnd, msg, wparam, lparam);
			}
			
			return DefWindowProc(hwnd, msg, wparam, lparam);
		}

		// Interface of window

		bool IsWindowClosed(WindowID id)
		{
			return GetInfoFromId(id).isClosed;
		}

		void ResizeWindow(const WindowInfo& info, const RECT& area)
		{
			RECT windowRect{ area };
			AdjustWindowRect(&windowRect, info.style, FALSE);

			const s32 width{ windowRect.right - windowRect.left };
			const s32 height{ windowRect.bottom - windowRect.top };

			MoveWindow(info.hwnd, info.topLeft.x, info.topLeft.y, width, height, true);

		}

		void ResizeWindow(WindowID id, u32 width, u32 height)
		{
			WindowInfo& info{ GetInfoFromId(id) };

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

		Math::u32v4 GetWindowSize(WindowID id)
		{
			WindowInfo& info{ GetInfoFromId(id) };
			RECT& area{ info.isFullScreen ? info.fullScreenArea : info.clientArea };
			return { (u32)area.left, (u32)area.top, (u32)area.right, (u32)area.bottom };
		}
		
		void SetWindowCaption(WindowID id, const wchar_t* caption)
		{
			WindowInfo& info{ GetInfoFromId(id) };
			SetWindowText(info.hwnd, caption);

		}

		WindowHandle GetWindowHandle(WindowID id)
		{
			return GetInfoFromId(id).hwnd;
		}

		bool IsWindowFullScreen(WindowID id)
		{
			return GetInfoFromId(id).isFullScreen;
		}

		void SetWindowFullScreen(WindowID id, bool isFullScreen)
		{
			WindowInfo& info{ GetInfoFromId(id) };
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

	}// annoymous namespace

	Window CreateNewWindow(const WindowInitInfo* const initInfo)
	{
		WindowProc callback{ initInfo ? initInfo->callback : nullptr };
		WindowExitResize resizeCallback {initInfo ? initInfo->resizeCallback : nullptr };
		WindowHandle parent{ initInfo ? initInfo->parent : nullptr };

		// set up window class

		WNDCLASSEX wc;
		ZeroMemory(&wc, sizeof(wc));
		wc.cbSize = sizeof(WNDCLASSEX);
		wc.style = CS_HREDRAW | CS_VREDRAW;
		wc.lpfnWndProc = InternalWindowProc;
		wc.cbClsExtra = 0;
		wc.cbWndExtra = sizeof(WindowProc) + sizeof(WindowExitResize);
		wc.hInstance = 0;
		wc.hIcon = LoadIcon(NULL, IDI_APPLICATION);
		wc.hCursor = LoadCursor(NULL, IDC_ARROW);
		wc.hbrBackground = CreateSolidBrush(RGB(26, 48, 76));
		wc.lpszMenuName = NULL;
		wc.lpszClassName = L"NebulaWindow";
		wc.hIconSm = LoadIcon(NULL, IDI_APPLICATION);

		// register window class 


		RegisterClassExW(&wc);

		// create an instance of window class

		WindowInfo info{ };

		info.clientArea.right = (initInfo && initInfo->width) ? info.clientArea.left + initInfo->width : info.clientArea.right;
		info.clientArea.bottom = (initInfo && initInfo->height) ? info.clientArea.top + initInfo->height : info.clientArea.bottom;
		info.style |= parent ? WS_CHILD : WS_OVERLAPPEDWINDOW;

		RECT rect{ info.clientArea };

		AdjustWindowRect(&rect, info.style, FALSE);

		const wchar_t* caption{ (initInfo && initInfo->caption) ? initInfo->caption : L"Nebula" };
		const s32 left{ (initInfo) ? initInfo->left : info.topLeft.x };
		const s32 top{ (initInfo) ? initInfo->top : info.topLeft.y };
		const s32 width{ rect.right - rect.left };
		const s32 height{ rect.bottom - rect.top };

		info.hwnd = CreateWindowEx(
			/* DWORD dwExStyle */        0,
			/* LPCTSTR lpClassName */    wc.lpszClassName,
			/* LPCTSTR lpWindowName */   caption,
			/* DWORD dwStyle */          info.style,
			/* int x */                  left,
			/* int y */                  top,
			/* int nWidth */             width,
			/* int nHeight */            height,
			/* HWND hWndParent */        parent,
			/* HMENU hMenu */            NULL,
			/* HINSTANCE hInstance */    NULL,
			/* LPVOID lpParam */         NULL
		);

		if (info.hwnd)
		{
			DEBUG_OP(SetLastError(0));

			const WindowID id{ AddToWindows(info) };

			SetWindowLongPtr(info.hwnd, GWLP_USERDATA, (LONG_PTR)id);

			if (callback) SetWindowLongPtr(info.hwnd, WINDOW_PROC_CALLBACK, (LONG_PTR)callback);

			if (resizeCallback) SetWindowLongPtr(info.hwnd, WINDOW_RESIZE_CALLBACK, (LONG_PTR)resizeCallback);

			assert(GetLastError() == 0);

			ShowWindow(info.hwnd, SW_NORMAL);
			UpdateWindow(info.hwnd);
			return Window{ id };
		}

		return {};
	}


	void RemoveWindow(WindowID id)
	{
		WindowInfo& info{ GetInfoFromId(id) };
		DestroyWindow(info.hwnd);
		RemoveFromWindows(id);
	}

	u32 GetWindowID(WindowHandle handle)
	{
		const WindowID id{ static_cast<WindowID>(GetWindowLongPtr(handle, GWLP_USERDATA)) };
		return id;
	}

	void SetWindowResizeCallbackInternal(WindowID id, WindowExitResize callback)
	{
		WindowInfo& info{ GetInfoFromId(id) };
		SetWindowLongPtr(info.hwnd, WINDOW_RESIZE_CALLBACK, (LONG_PTR)callback);
	}
	
#else

#error "platform not be implement"

#endif


	bool Window::IsValid() const
	{
		return m_ID == InvalidID;
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
		return GetWindowHandle(m_ID);
	}

	void Window::SetCaption(const wchar_t* caption) const
	{
		assert(IsValid());
		SetWindowCaption(m_ID, caption);
	}

	 Math::u32v4 Window::Size() const
	{
		assert(IsValid());
		return GetWindowSize(m_ID);
	}

	void Window::Resize(u32 width, u32 height) const
	{
		assert(IsValid());
		ResizeWindow(m_ID, width, height);
	}

	 u32 Window::Width() const
	{
		assert(IsValid());
		Math::u32v4 s{ Size() };
		return s.z - s.x;
	}

	 u32 Window::Height() const
	{
		assert(IsValid());
		Math::u32v4 s{ Size() };
		return s.w - s.y;
	}

	bool Window::IsClosed() const
	{
		assert(IsValid());
		return IsWindowClosed(m_ID);
	}
	
}