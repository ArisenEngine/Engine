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
template<class T>
using Ptrs = std::vector<Ptr<T>>;
#define MakePtr(ClassName,...) std::make_shared<ClassName>(##__VA_ARGS__)

template<class T>
using UniquePtr = std::unique_ptr<T>;
template<typename T>
using UniquePtrs = std::vector<UniquePtr<T>>;
#define MakeUniquePtr(ClassName,...) std::make_unique<ClassName>(##__VA_ARGS__)

// TODO: add log types.
#define ADD_RHI_HEAD(Message) std::format("[RHI]:{}",Message)
#define LOG_RHI_DEBUG(Message, ...)\
    LOG_DEBUG(ADD_RHI_HEAD(Message));
#define LOG_RHI_INFO(Message, ...)\
    LOG_INFO(ADD_RHI_HEAD(Message));
#define LOG_RHI_WARNING(Message, ...)\
    LOG_WARNING(ADD_RHI_HEAD(Message));
#define LOG_RHI_ERROR(Message, ...)\
    LOG_ERROR(ADD_RHI_HEAD(Message));
#define LOG_RHI_FATAL(Message, ...)\
    LOG_FATAL(ADD_RHI_HEAD(Message));
#define LOG_RHI_FATAL_AND_THROW(Message, ...)\
    LOG_FATAL_AND_THROW(ADD_RHI_HEAD(Message))
#define LOG_RHI_CONSTRUCTOR(ClassNameStr,...)\
    LOG_RHI_DEBUG(std::format("<{}> Constructor!", ClassNameStr));
#define LOG_RHI_DESTRUCTOR(ClassNameStr,...)\
    LOG_RHI_DEBUG(std::format("<{}> Destructor!", ClassNameStr));
