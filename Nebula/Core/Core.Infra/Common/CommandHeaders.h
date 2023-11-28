#pragma once

// C/C++
#include <stdint.h>
#include <assert.h>
#include <math.h>
#include <memory>
#include <typeinfo>
#include <iostream>
#include <fstream>
#include <wchar.h>

#if _WIN64
#include<DirectXMath.h>

/// NOTE: Windows.h should included after boost lib
#include<Windows.h>
#endif

// common headers
#include"PrimitiveTypes.h"
#include"Id.h"
#include"MathTypes.h"
#include"../Containers/Containers.h"

// RHI
#include "../RHI/Devices/Device.h"
#include "../RHI/Surfaces/Surface.h"
#include "../Platforms/GraphsicsAPI.h"

#ifdef _DEBUG

#define DEBUG_OP(x) x

#else

#define DEBUG_OP(x) ((void)0)

#endif

#define NO_COPY_NO_MOVE_NO_DEFAULT(type_name)       \
        type_name() = delete;                       \
        type_name(const type_name&) = delete;       \
        type_name(type_name&&) = delete;            \

#define NO_COPY_NO_MOVE(type_name)          \
type_name(const type_name&) = delete;       \
type_name(type_name&&) = delete;            \

#define NO_COPY(type_name)                  \
type_name(type_name&&) = delete;            \

#define NO_MOVE(type_name)                  \
type_name(const type_name&) = delete;       \

#define NO_COMPARE(type_name)  type_name& operator=(const type_name&) = delete;




