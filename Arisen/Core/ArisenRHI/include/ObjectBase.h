#pragma once
#include "IObject.h"

ARISENRHI_BEGIN_NAMEPSACE
class ObjectBase : public virtual IObject
{
public:
    virtual bool SetName(std::string_view name) override;
private:
    std::string m_name;
};



ARISENRHI_END_NAMESPACE
