#pragma once
#include <memory>

template<typename T>
class SharedPtrOutWrapper
{
public:
    SharedPtrOutWrapper(std::shared_ptr<T>& ptr);

    ~SharedPtrOutWrapper();

    T** operator&();
private:
    std::shared_ptr<T>& ptrRef;
    T* rawPtr;
};


