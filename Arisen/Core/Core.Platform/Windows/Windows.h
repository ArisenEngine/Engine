#pragma once
#include "../CorePlatformCommon.h"
#include "Common/PrimitiveTypes.h"

namespace ArisenEngine::Platforms
{
	using WindowID = UInt32;

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
		Math::UInt32Vector4 Size() const;
		void Resize(UInt32 width, UInt32 height) const;
		UInt32 Width() const;
		UInt32 Height() const;
		bool IsClosed() const;

	private:

		WindowID m_ID{InvalidID};
	};

	
}
