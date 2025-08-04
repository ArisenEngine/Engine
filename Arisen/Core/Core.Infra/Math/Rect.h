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
        :mWidth(RoundCast<T>(w))
        ,mHeight(RoundCast<T>(h))
        {
            if constexpr(std::is_signed_v<V>)
            {
                CHECK_GREATER_OR_EQUAL(mWidth, 0, "width cannot be less than 0!");
                CHECK_GREATER_OR_EQUAL(mHeight, 0, "height cannot be less than 0!");
            }
        }

        T GetWidth() const noexcept {return mWidth;}
        T GetHeight() const noexcept {return mHeight;}
    private:
        T mWidth{};
        T mHeight{};
    };

    class Rect
    {
    public:
    };
}
