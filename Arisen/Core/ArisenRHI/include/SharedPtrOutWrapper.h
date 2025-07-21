#pragma once
#include <memory>

#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename T>
class SharedPtrOutWrapper
{
public:
    SharedPtrOutWrapper(std::shared_ptr<T>& ptr)
    :ptrRef(ptr), rawPtr(nullptr)
    {
        ptrRef.reset();
    }
    
    ~SharedPtrOutWrapper()
    {
        if (rawPtr)
        {
            ptrRef.reset(rawPtr);
        }
    }
    
    T** operator&()
    {
        return &rawPtr;
    }
private:
    std::shared_ptr<T>& ptrRef;
    T* rawPtr;
};
ARISENRHI_END_NAMESPACE
