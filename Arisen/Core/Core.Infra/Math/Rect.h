#pragma once
#include <type_traits>

#include "Math.h"
#include "Common/Checks.h"

namespace ArisenEngine::Math
{
    template <typename T> requires std::is_arithmetic_v<T>
    class RectSize
    {
    public:
        RectSize() = default;
        template<typename V>
        RectSize(V w, V h) noexcept(std::is_unsigned_v<V>)
        :m_width(RoundCast<T>(w))
        ,m_height(RoundCast<T>(h))
        {
            if constexpr(std::is_signed_v<V>)
            {
                CHECK_GREATER_OR_EQUAL(m_width, 0, "width cannot be less than 0!");
                CHECK_GREATER_OR_EQUAL(m_height, 0, "height cannot be less than 0!");
            }
        }
        
    private:
        T m_width{};
        T m_height{};
    };

    class Rect
    {
    public:
    };
}
