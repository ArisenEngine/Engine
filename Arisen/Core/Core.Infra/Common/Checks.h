#pragma once
#include <stdexcept>

#define CHECK_GREATER_OR_EQUAL(arg, min_value, log)\
    do\
    {\
        if(arg < static_cast<std::decay_t<decltype(arg)>>(min_value))\
            throw std::runtime_error(log);\
   }while(0)