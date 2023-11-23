#pragma once

// C/C++
#include<stdint.h>
#include<assert.h>
#include<math.h>
#include<memory>
#include<typeinfo>

// BOOST
#ifdef USE_BOOST 

#include <iostream>
#include <regex>

#define DBOOST_STACKTRACE_USE_ADDR2LINE

#include <boost/filesystem.hpp>
#include <boost/log/core.hpp>
#include <boost/log/trivial.hpp>
#include <boost/stacktrace.hpp>
#include <boost/log/expressions.hpp>
#include <boost/log/utility/setup/file.hpp>
#include <boost/log/utility/setup/common_attributes.hpp>


#endif

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
