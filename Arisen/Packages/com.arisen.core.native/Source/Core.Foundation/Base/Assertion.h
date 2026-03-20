#pragma once

#include "BasicMacros.h"
#include <cstdlib>

namespace ArisenEngine
{
    // High-performance assertion failure reporter
    // Implemented in Assertion.cpp to avoid circular dependencies with Log
    FOUNDATION_DLL void ReportAssertionFailure(const char* condition, const char* file, int line, const char* function,
                                               const char* msg = nullptr);

    // Initialize the assertion system, including CRT hooks
    FOUNDATION_DLL void InitAssertionSystem();
}

#undef assert
#ifdef _DEBUG
#define assert(condition)                                                   \
    do {                                                                    \
        if (!(condition)) {                                                 \
            ::ArisenEngine::ReportAssertionFailure(#condition, __FILE__, __LINE__, __FUNCTION__); \
            std::abort();                                                   \
        }                                                                   \
    } while (0)
#else
    #define assert(condition) ((void)0)
#endif

#ifndef ASSERT
#define ASSERT(x) DEBUG_OP(assert(x))
#endif

// Specialized assertion with a custom message
#define ASSERT_MSG(condition, msg) \
    do { \
        if (!(condition)) { \
            ::ArisenEngine::ReportAssertionFailure(#condition, __FILE__, __LINE__, __FUNCTION__, msg); \
            std::abort(); \
        } \
    } while (0)

#define ENGINE_ASSERT(condition, msg) ASSERT_MSG(condition, msg)
