#include "Platform.h"
#include "PlatformTypes.h"
#include "Common/CommandHeaders.h"



namespace NebulaEngine::Platforms
{
	using namespace NebulaEngine::Containers;

#ifdef _WIN64

	namespace
	{
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


		vector<WindowInfo> windows;
		vector<u32> availableSlots;

		u32 AddToWindows(WindowInfo info)
		{
			u32 id{ ID::InvalidID };
			if (availableSlots.empty())
			{
				id = (ID::IdType)windows.size();
				windows.emplace_back(info);
			}
			else
			{
				id = availableSlots.back();
				availableSlots.pop_back();
				assert(id != u32_invalid_id);
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
			const WindowID id{ (ID::IdType)GetWindowLongPtr(handle, GWLP_USERDATA) };
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

			switch (msg)
			{
			case WM_DESTROY:
				GetInfoFromHandle(hwnd).isClosed = true;
				break;


			case WM_EXITSIZEMOVE:
				info = &GetInfoFromHandle(hwnd);
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
			}
			LONG_PTR longPtr{ GetWindowLongPtr(hwnd, 0) };
			return longPtr ? ((WindowProc)longPtr)(hwnd, msg, wparam, lparam) : DefWindowProc(hwnd, msg, wparam, lparam);
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

			RECT& area{ info.isFullScreen ? info.fullScreenArea : info.clientArea };

			area.bottom = area.top + height;
			area.right = area.left + width;

			ResizeWindow(info, area);

		}

		Math::u32v4 GetWindowSize(WindowID id)
		{
			WindowInfo& info{ GetInfoFromId(id) };
			RECT area{ info.isFullScreen ? info.fullScreenArea : info.clientArea };
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
		WindowHandle parent{ initInfo ? initInfo->parent : nullptr };

		// set up window class

		WNDCLASSEX wc;
		ZeroMemory(&wc, sizeof(wc));
		wc.cbSize = sizeof(WNDCLASSEX);
		wc.style = CS_HREDRAW | CS_VREDRAW;
		wc.lpfnWndProc = InternalWindowProc;
		wc.cbClsExtra = 0;
		wc.cbWndExtra = callback ? sizeof(callback) : 0;
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
		RECT rc{ info.clientArea };

		AdjustWindowRect(&rc, info.style, FALSE);

		const wchar_t* caption{ (initInfo && initInfo->callback) ? initInfo->caption : L"Nebula" };
		const s32 left{ (initInfo && initInfo->left) ? initInfo->left : info.clientArea.left };
		const s32 top{ (initInfo && initInfo->top) ? initInfo->top : info.clientArea.top };
		const s32 width{ (initInfo && initInfo->width) ? initInfo->width : rc.right - rc.left };
		const s32 height{ (initInfo && initInfo->height) ? initInfo->height : rc.bottom - rc.top };

		info.style |= parent ? WS_CHILD : WS_OVERLAPPEDWINDOW;

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
			SetLastError(0);

			const WindowID id{ AddToWindows(info) };

			SetWindowLongPtr(info.hwnd, GWLP_USERDATA, (LONG_PTR)id);

			if (callback) SetWindowLongPtr(info.hwnd, 0, (LONG_PTR)callback);

			assert(GetLastError() == 0);

			ShowWindow(info.hwnd, SW_DENORMAL);
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

#else

#error "platform not be implement"

#endif


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

	const Math::u32v4 Window::Size() const
	{
		assert(IsValid());
		return GetWindowSize(m_ID);
	}

	void Window::Resize(u32 width, u32 height) const
	{
		assert(IsValid());
		ResizeWindow(m_ID, width, height);
	}

	const u32 Window::Width() const
	{
		assert(IsValid());
		Math::u32v4 s{ Size() };
		return s.z - s.x;
	}

	const u32 Window::Height() const
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