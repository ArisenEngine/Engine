#pragma once
#include "Logger.h"

#ifdef _DEBUG

#define ASSERTION_FAILED(Message, ...)    \
do                                  \
{                                   \
LOG_FATAL_AND_THROW(Message);             \
} while (0)

#define CHECK(Expr, Message, ...)    \
do                          \
{                           \
if (!(Expr))            \
{                       \
ASSERTION_FAILED(Message,##__VA_ARGS__); \
}                       \
} while (false)    

#define UNEXPECTED_RETURN(argument, ReturnVal)\
    do{\
    ASSERTION_FAILED("Unexpected return value occured!");\
    throw std::runtime_error("Unexpected return value occured!");\
    }while(0)

#define CHECK_VALID(arg)\
    do{\
    CHECK(arg != nullptr,"arg is nullptr!");\
    }while(0)


#else

#define ASSERTION_FAILED(Message, ...) do{}while(false)
#define CHECK(Expr, Message, ...) do{} while(false)
#define UNEXPECTED_RETURN(argument, ReturnVal) do{}while(false)
#endif

