#pragma once
#include "CommandHeaders.h"
#include "Containers/Containers.h"

namespace NebulaEngine
{
	// unsigned integers

	using u64 = uint64_t;
	using u32 = uint32_t;
	using u16 = uint16_t;
	using u8 = uint8_t;

	// signed integers

	using s64 = int64_t;
	using s32 = int32_t;
	using s16 = int16_t;
	using s8 = int8_t;

	constexpr u64   u64_invalid_id{ 0xffff'ffff'ffff'ffffui64 };
	constexpr u32   u32_invalid_id{ 0xffff'ffffui32 };
	constexpr u16   u16_invalid_id{ 0xffffui16 };
	constexpr u8    u8_invalid_id{ 0xffui8 };




	using f32 = float;


	namespace String
	{
		// std::wstring to std::string
		inline std::string WStringToString(const std::wstring& wstr) {
			std::mbstate_t state = std::mbstate_t();
			const wchar_t* src = wstr.data();
			size_t len = 0;
			wcsrtombs_s(&len, nullptr, 0, &src, 0, &state);  // fetch converted length

			Containers::Vector<char> dst(len);  // create buffer
			wcsrtombs_s(&len, dst.data(), dst.size(), &src, dst.size(), &state);
			return std::string(dst.data());
		}

		// std::string to std::wstring
		inline std::wstring StringToWString(const std::string& str)
		{
			std::mbstate_t state = std::mbstate_t();
			const char* src = str.data();
			size_t len = 0;
			mbsrtowcs_s(&len, nullptr, 0, &src, 0, &state);  // fetch converted length

			Containers::Vector<wchar_t> dst(len);  // create buffer
			mbsrtowcs_s(&len, dst.data(), dst.size(), &src, dst.size(), &state);
			return std::wstring(dst.data());
		}
	}
	
}
