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
#include <string>
#include <algorithm>
#include <optional>

#if _WIN64
#include<DirectXMath.h>

/// NOTE: Windows.h should included after boost lib
#include<Windows.h>
// TODO: 需要考虑别的平台
#define VK_USE_PLATFORM_WIN32_KHR 1


#endif

// common headers
#include"PrimitiveTypes.h"
#include"Id.h"
#include"MathTypes.h"
#include"../Containers/Containers.h"

#ifdef _DEBUG

#define DEBUG_OP(x) x

#else

#define DEBUG_OP(x) ((void)0)

#endif

#define NO_COPY_NO_MOVE_NO_DEFAULT(type_name)       \
        type_name() = delete;                       \
        type_name(const type_name&) = delete;       \
        type_name(type_name&&) = delete;            \

#define NO_COPY_NO_DEFAULT(type_name)       \
type_name() = delete;                       \
type_name(const type_name&) = delete;       \


#define NO_COPY_NO_MOVE(type_name)          \
type_name(const type_name&) = delete;       \
type_name(type_name&&) = delete;            \

#define NO_COPY(type_name)                  \
type_name(type_name&&) = delete;            \

#define NO_MOVE(type_name)                  \
type_name(const type_name&) = delete;       \

#define NO_DEFAULT(type_name)                \
type_name() = delete;       \

#define VIRTUAL_DECONSTRUCTOR(type_name) virtual ~type_name() noexcept { }

#define NO_COMPARE(type_name)  type_name& operator=(const type_name&) = delete;

#define ASSERT assert

#include "../RHI/Enums/ComponentSwizzle.h"
#include "../RHI/Enums/MemoryViewType.h"
#include "../RHI/Enums/Format.h"
#include "../RHI/Enums/PresentMode.h"
#include "../RHI/Enums/ColorSpace.h"
#include "../RHI/Enums/SharingMode.h"
#include "../RHI/Enums/SurfaceTransformFlagBits.h"

// RHI
#include "../RHI/Devices/Device.h"
#include "../RHI/Surfaces/Surface.h"
#include "../RHI/Instance.h"
#include "../Platforms/GraphsicsAPI.h"
