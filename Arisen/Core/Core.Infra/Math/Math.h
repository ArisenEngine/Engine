#pragma once
#include <cmath>
#include <type_traits>

namespace ArisenEngine::Math
{
    template<typename T, typename V> requires std::is_arithmetic_v<T> && std::is_arithmetic_v<V>
    constexpr T RoundCast(V value) noexcept
    {
        if constexpr(std::is_same_v<T, V>)
            return value;
        else
        {
            if constexpr(std::is_integral_v<T> && std::is_floating_point_v<V>)
                return static_cast<T>(std::round(value));
            else
                return static_cast<T>(value);
        }
    }
}
