#pragma once
#include "../CorePlatformCommon.h"
#include "Common/PrimitiveTypes.h"

namespace NebulaEngine::Platforms
{
	using WindowID = u32;

	class Window
	{
	public:

		 explicit Window(WindowID id);
		 Window();
		 WindowID ID() const;
		bool IsValid() const;

		void SetFullScreen(bool isFullScreen) const;
		bool IsFullScreen() const;
		void* Handle() const;
		void SetCaption(const wchar_t* caption) const;
		Math::u32v4 Size() const;
		void Resize(u32 width, u32 height) const;
		u32 Width() const;
		u32 Height() const;
		bool IsClosed() const;

	private:

		WindowID m_ID{InvalidID};
	};

	
}
