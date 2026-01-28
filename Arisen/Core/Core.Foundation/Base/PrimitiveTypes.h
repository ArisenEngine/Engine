#pragma once

#include <cstdint>

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
}
