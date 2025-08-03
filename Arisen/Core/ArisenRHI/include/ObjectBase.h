#pragma once
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
class ObjectBase : public virtual IObject
{
public:
    ObjectBase() =default;
    explicit ObjectBase(std::string_view name);
    
    virtual bool SetName(std::string_view name) override;
    [[nodiscard]] virtual std::string_view GetName() const noexcept override{return m_name;}

private:
    std::string m_name;
};



ARISENRHI_END_NAMESPACE
