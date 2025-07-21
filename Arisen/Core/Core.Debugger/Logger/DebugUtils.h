#pragma once
#include "Logger.h"

#ifdef ARISEN_DEBUG





#else

// debug log func and line         
#define ASSERTION_FAILED(Message, ...)    \
    do                                  \
    {                                   \
        LOG_FATAL(Message);             \
    } while (0)

#define CHECK(Expr, Message, ...)    \
    do                          \
    {                           \
        if (!(Expr))            \
        {                       \
            ASSERTION_FAILED(Message,##__VA_ARGS__); \
        }                       \
    } while (false)             
#endif

