#pragma once
#include "CommandHeaders.h"
#include "../Containers/Containers.h"

namespace ArisenEngine
{
	// unsigned integers

	using UInt64 = uint64_t;
	using UInt32 = uint32_t;
	using UInt16 = uint16_t;
	using UInt8 = uint8_t;

	// signed integers

	using SInt64 = int64_t;
	using SInt32 = int32_t;
	using SInt16 = int16_t;
	using SInt8 = int8_t;

	constexpr UInt64 u64Invalid = 0xffff'ffff'ffff'ffffULL;
	constexpr UInt32 u32Invalid = 0xffff'ffffUL;
	constexpr UInt16 u16Invalid = static_cast<uint16_t>(0xffff);
	constexpr UInt8  u8Invalid  = static_cast<uint8_t>(0xff);


	using Float32 = float;

	constexpr UInt32 InvalidID =  0xffff'ffffUL;

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
