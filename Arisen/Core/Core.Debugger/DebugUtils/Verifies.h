#pragma once
#include <stdexcept>
#include "../Logger/Logger.h"

// verify is a macro which annot be ignored even in release mode.

#define LOG_AND_THROW(log)\
    do{\
        LOG_FATAL_AND_THROW(log);\
      }while(0)

#define VERIFY_GREATER_OR_EQUAL(arg, min_value, log)\
    do\
    {\
        if(arg < static_cast<std::decay_t<decltype(arg)>>(min_value))\
           {LOG_AND_THROW(std::format("GreaterOrEqual verify failed:{}",log));}\
   }while(0)

#define VERIFY_LESS(arg, compare_val, ...)\
    do{\
        if(arg >= static_cast<std::decay_t<decltype(arg)>>(compare_val))\
            {LOG_AND_THROW(std::format("less verify failed:{}",##__VA_ARGS__));}\
    }while(0)

#define VERIFY_NOT_NULL(arg, log) if(arg == nullptr) LOG_AND_THROW(log)