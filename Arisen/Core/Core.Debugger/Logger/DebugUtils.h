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

#else

#define ASSERTION_FAILED(Message, ...) do{}while(false)
#define CHECK(Expr, Message, ...) do{} while(false)
#endif

