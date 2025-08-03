#pragma once
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE

struct IObject
    :public std::enable_shared_from_this<IObject>
{
    virtual ~IObject() = default;
    // ref or interface stuffs.
    
    virtual bool SetName(std::string_view name) = 0;
    [[nodiscard]] virtual std::string_view GetName() const noexcept = 0;

    // todo : change to ref count ptr.
    [[nodiscard]] virtual Ptr<IObject> AsObjectPtr(){return shared_from_this();};
    
    template<typename T> requires std::is_base_of_v<IObject, T>
    [[nodiscard]] Ptr<T> GetInterface(){return std::static_pointer_cast<T>(AsObjectPtr());}
};

ARISENRHI_END_NAMESPACE
