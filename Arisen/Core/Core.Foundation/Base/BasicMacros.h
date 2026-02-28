#pragma once

#include "../CoreFoundationCommon.h"

// Memory/Ownership macros
#define NO_COPY_NO_MOVE_NO_DEFAULT(type_name)       \
        type_name() = delete;                       \
        type_name(const type_name&) = delete;       \
        type_name(type_name&&) = delete;
#define NO_COPY_NO_DEFAULT(type_name)               \
        type_name() = delete;                       \
        type_name(const type_name&) = delete;
#define NO_COPY_NO_MOVE(type_name)                  \
        type_name(const type_name&) = delete;       \
        type_name(type_name&&) = delete;
#define NO_COPY(type_name)                          \
        type_name(const type_name&) = delete;
#define NO_MOVE(type_name)                          \
        type_name(type_name&&) = delete;
#define NO_DEFAULT(type_name)                       \
        type_name() = delete;
#define VIRTUAL_DECONSTRUCTOR(type_name) virtual ~type_name() noexcept = default;

#define NO_COMPARE(type_name)  type_name& operator=(const type_name&) = delete;

// Utility macros
#ifndef EXECUTE_CODE
#define EXECUTE_CODE(code) do { code; } while (0)
#endif

#ifndef DEBUG_OP
#ifdef _DEBUG
#define DEBUG_OP(x) x
#else
#define DEBUG_OP(x) ((void)0)
#endif
#endif
