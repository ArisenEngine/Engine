#pragma once
#include "CoreMinimalRHI.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename E, typename M = std::underlying_type_t<E>>
class EnumMask
{
    static_assert(std::is_enum_v<E>, "EnumMask must be an enum type");
    static_assert(std::is_integral_v<M>, "EnumMask mask type must be integer type");
    static_assert(sizeof(E) <= sizeof(M), "EnumMask mask type is too large");
    static_assert(std::is_unsigned_v<M>, "EnumMask mask type must be unsigned");
    
public:
    class Bit
    {
    public:
        explicit constexpr Bit(M i) noexcept
            :m_value(static_cast<M>(M{1} << i)){}
        
        constexpr Bit(E e) noexcept
            :Bit(static_cast<M>(e)){}

        constexpr M GetValue() const noexcept {return m_value;}
    private:
        const M m_value;
    };

    constexpr EnumMask() = default;
    constexpr EnumMask(std::initializer_list<Bit> bits) noexcept
        :m_value(BitsToInt(bits.begin(), bits.end())){}

private:
    constexpr static M BitsToInt(typename std::initializer_list<Bit>::const_iterator it,
        typename std::initializer_list<Bit>::const_iterator end) noexcept
    {
        return it == end ? M{0} : it->GetValue() | BitsToInt(it + 1, end);  
    }
    
private:
    M m_value{};
};
ARISENRHI_END_NAMESPACE