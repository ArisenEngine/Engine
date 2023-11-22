#pragma once
#include"Common/CommandHeaders.h"
#include "../Common.h"

namespace NebulaEngine::API
{
	DEFINE_TYPED_ID(WindowID)

	class Window
	{
	public:

		constexpr explicit Window(WindowID id) : m_ID{id} {}
		constexpr Window() : m_ID{ ID::InvalidID } {}
		constexpr WindowID ID() const { return m_ID; }
		bool IsValid() const { return ID::IsValid(m_ID); }

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

		WindowID m_ID{ID::InvalidID};
	};

}