#pragma once

#include <type_traits>

template<typename E>
constexpr bool IsEnumClass = std::is_enum_v<E> && !std::is_convertible_v<E, int>;

template<typename E>
constexpr auto to_underlying(E e) noexcept
{
    static_assert(std::is_enum_v<E>, "to_underlying only works with enum types");
    return static_cast<std::underlying_type_t<E>>(e);
}

template<typename E>
constexpr std::enable_if_t<IsEnumClass<E>, E>
operator |(E lhs, E rhs)
{
    return static_cast<E>(to_underlying(lhs) | to_underlying(rhs));
}

template<typename E>
constexpr std::enable_if_t<IsEnumClass<E>, E>
operator&(E lhs, E rhs)
{
    return static_cast<E>(to_underlying(lhs) & to_underlying(rhs));
}

template<typename E>
constexpr std::enable_if_t<IsEnumClass<E>, E>
operator^(E lhs, E rhs)
{
    return static_cast<E>(to_underlying(lhs) ^ to_underlying(rhs));
}

template<typename E>
constexpr std::enable_if_t<IsEnumClass<E>, E>
operator~(E val)
{
    return static_cast<E>(~to_underlying(val));
}


template<typename E>
inline std::enable_if_t<IsEnumClass<E>, E&>
operator|=(E& lhs, E rhs)
{
    lhs = lhs | rhs;
    return lhs;
}

template<typename E>
inline std::enable_if_t<IsEnumClass<E>, E&>
operator&=(E& lhs, E rhs)
{
    lhs = lhs & rhs;
    return lhs;
}

template<typename E>
inline std::enable_if_t<IsEnumClass<E>, E&>
operator^=(E& lhs, E rhs)
{
    lhs = lhs ^ rhs;
    return lhs;
}

template<typename E>
inline std::enable_if_t<IsEnumClass<E>, bool>
HasFlag(E value, E flag)
{
    using U = std::underlying_type_t<E>;
    return (static_cast<U>(value) & static_cast<U>(flag)) != 0;
}

template<typename E>
inline std::enable_if_t<IsEnumClass<E>, int32_t>
ToInt32(E value)
{
    return static_cast<int32_t>(value);
}