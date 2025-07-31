#pragma once
#include <memory>
#include "Logger/Logger.h"


#define ARISENRHI_BEGIN_NAMEPSACE \
    namespace ArisenRHI\
    {

#define ARISENRHI_END_NAMESPACE\
    }

// TODO: refactor to ref count object and memory allocator.
template<class T>
using Ptr = std::shared_ptr<T>;
// #define Ptr(ClassName) std::shared_ptr<ClassName>
#define MakePtr(ClassName,...) std::make_shared<ClassName>(##__VA_ARGS__)

// TODO: add log types.
#define LOG_RHI_DEBUG(Message, ...)\
    LOG_DEBUG(Message)
#define LOG_RHI_INFO(Message, ...)\
    LOG_INFO(Message)
#define LOG_RHI_WARNING(Message, ...)\
    LOG_WARNING(Message)
#define LOG_RHI_ERROR(Message, ...)\
    LOG_ERROR(Message)
#define LOG_RHI_FATAL(Message, ...)\
    LOG_FATAL(Message)
#define LOG_RHI_FATAL_AND_THROW(Message, ...)\
    LOG_FATAL_AND_THROW(Message)
