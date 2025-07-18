#include "SharedPtrOutWrapper.h"

template <typename T>
SharedPtrOutWrapper<T>::SharedPtrOutWrapper(std::shared_ptr<T>& ptr)
    :ptrRef(ptr), rawPtr(nullptr)
{
    ptrRef.reset();
}

template <typename T>
SharedPtrOutWrapper<T>::~SharedPtrOutWrapper()
{
    if (rawPtr)
    {
        ptrRef.reset(rawPtr);
    }
}

template <typename T>
T** SharedPtrOutWrapper<T>::operator&()
{
    return &rawPtr;
}