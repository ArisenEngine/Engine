#pragma once
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename BaseInterface>
class ObjectBase : public BaseInterface
{
public:
    ObjectBase() =default;
    explicit ObjectBase(std::string_view name)
    :m_name(name)
    {}

    virtual ~ObjectBase() = default;
    
    virtual bool SetName(std::string_view name) override
    {
        if (m_name == name)
        {
            return false;
        }

        // TODO : name changed.

        m_name = name;
        return true;
    }

    [[nodiscard]] virtual std::string_view GetName() const noexcept override{return m_name;}

private:
    std::string m_name;
};



ARISENRHI_END_NAMESPACE
